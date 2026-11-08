using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmsService.Domain;

namespace SmsService.Application;

/// <summary>
/// Core send/poll logic, independent of hosting so it can be unit tested.
/// The BackgroundService (Infrastructure) calls <see cref="RunCycleAsync"/> every PollIntervalSeconds.
/// </summary>
public class MessageDispatcher
{
    private readonly IAppDbContext _db;
    private readonly ISmsProviderClient _provider;
    private readonly MessageProcessingOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<MessageDispatcher> _logger;

    public MessageDispatcher(
        IAppDbContext db,
        ISmsProviderClient provider,
        IOptions<MessageProcessingOptions> options,
        TimeProvider clock,
        ILogger<MessageDispatcher> logger)
    {
        _db = db;
        _provider = provider;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task RunCycleAsync(CancellationToken ct)
    {
        await SendPendingAsync(ct);
        await PollSentAsync(ct);
    }

    /// <summary>Pending -> Sent (provider accepted) or Failed/SEND_FAILED (all attempts failed).</summary>
    public async Task SendPendingAsync(CancellationToken ct)
    {
        var pending = await _db.Messages
            .Include(m => m.Phone)
            .Where(m => m.Status == MessageStatus.Pending)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        foreach (var message in pending)
        {
            await SendWithRetryAsync(message, ct);
            // Save per message so a crash mid-batch loses at most one result.
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Sent -> Delivered / Failed (per provider), or Failed/DELIVERY_TIMEOUT after the configured time.</summary>
    public async Task PollSentAsync(CancellationToken ct)
    {
        var sent = await _db.Messages
            .Where(m => m.Status == MessageStatus.Sent)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        foreach (var message in sent)
        {
            await PollOneAsync(message, ct);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task SendWithRetryAsync(Message message, CancellationToken ct)
    {
        var maxAttempts = Math.Max(1, _options.MaxSendAttempts);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Only the phone number and body are sent to the provider.
                var providerId = await _provider.SendAsync(message.Phone.Number, message.Body, ct);
                message.ProviderMessageId = providerId;
                SetStatus(message, MessageStatus.Sent, null);
                _logger.LogInformation("Message {MessageId} sent (provider id {ProviderId}) on attempt {Attempt}",
                    message.Id, providerId, attempt);
                return;
            }
            catch (SmsProviderException ex) when (ex.IsTransient && attempt < maxAttempts)
            {
                var delay = GetRetryDelay(attempt);
                _logger.LogWarning(ex, "Send attempt {Attempt}/{Max} failed for message {MessageId}. Retrying in {Delay}",
                    attempt, maxAttempts, message.Id, delay);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, _clock, ct);
            }
            catch (SmsProviderException ex)
            {
                _logger.LogError(ex, "Sending message {MessageId} failed on attempt {Attempt}/{Max} (transient: {Transient})",
                    message.Id, attempt, maxAttempts, ex.IsTransient);
                break;
            }
        }

        SetStatus(message, MessageStatus.Failed, FailureReasons.SendFailed);
    }

    private async Task PollOneAsync(Message message, CancellationToken ct)
    {
        try
        {
            var status = await _provider.GetStatusAsync(message.ProviderMessageId!, ct);
            switch (status)
            {
                case ProviderStatus.Delivered:
                    SetStatus(message, MessageStatus.Delivered, null);
                    return;
                case ProviderStatus.Failed:
                    SetStatus(message, MessageStatus.Failed, FailureReasons.SendFailed);
                    return;
            }
        }
        catch (SmsProviderException ex)
        {
            // Decision 29: keep the status, log, try again next cycle. The delivery timeout is the safety net.
            _logger.LogWarning(ex, "Status poll failed for message {MessageId}; will retry next cycle", message.Id);
        }

        var deadline = message.CreatedAt.AddMinutes(_options.DeliveryTimeoutMinutes);
        if (_clock.GetUtcNow().UtcDateTime >= deadline)
        {
            SetStatus(message, MessageStatus.Failed, FailureReasons.DeliveryTimeout);
            _logger.LogWarning("Message {MessageId} timed out waiting for delivery", message.Id);
        }
    }

    private TimeSpan GetRetryDelay(int attempt)
    {
        var delays = _options.RetryDelaysSeconds ?? MessageProcessingOptions.DefaultRetryDelaysSeconds;
        if (delays.Length == 0) return TimeSpan.Zero;
        var seconds = delays[Math.Min(attempt - 1, delays.Length - 1)];
        return TimeSpan.FromSeconds(seconds);
    }

    private void SetStatus(Message message, MessageStatus status, string? failureReason)
    {
        message.Status = status;
        message.FailureReason = failureReason;
        message.UpdatedAt = _clock.GetUtcNow().UtcDateTime;
    }
}
