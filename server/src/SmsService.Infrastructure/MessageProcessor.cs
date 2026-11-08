using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmsService.Application;

namespace SmsService.Infrastructure;

/// <summary>
/// Background loop: every PollIntervalSeconds sends Pending messages and polls Sent ones.
/// Runs in-process with the API (fine for this scope, a known bottleneck at scale).
/// </summary>
public class MessageProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessageProcessingOptions _options;
    private readonly ILogger<MessageProcessor> _logger;

    public MessageProcessor(IServiceScopeFactory scopeFactory, IOptions<MessageProcessingOptions> options, ILogger<MessageProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)));
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<MessageDispatcher>();
                await dispatcher.RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad cycle (e.g. DB hiccup) kill the loop.
                _logger.LogError(ex, "Message processing cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
