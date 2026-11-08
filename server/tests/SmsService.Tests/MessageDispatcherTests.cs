using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SmsService.Application;
using SmsService.Domain;
using SmsService.Infrastructure;

namespace SmsService.Tests;

public class MessageDispatcherTests
{
    private readonly FakeClock _clock = new();
    private readonly Mock<ISmsProviderClient> _provider = new();
    private readonly MessageProcessingOptions _options = new()
    {
        MaxSendAttempts = 3,
        DeliveryTimeoutMinutes = 2,
        RetryDelaysSeconds = new[] { 0, 0, 0 }, // no real waiting in tests
    };

    private MessageDispatcher CreateDispatcher(AppDbContext db) =>
        new(db, _provider.Object, Options.Create(_options), _clock, NullLogger<MessageDispatcher>.Instance);

    private Message AddMessage(AppDbContext db, MessageStatus status, string number = "0591231234", string? providerId = null)
    {
        var phone = TestDb.AddEmployeeWithPhone(db, number);
        var message = new Message
        {
            Id = Guid.NewGuid(),
            PhoneId = phone.Id,
            Body = "שלום",
            Status = status,
            ProviderMessageId = providerId,
            CreatedAt = _clock.Now.UtcDateTime,
            UpdatedAt = _clock.Now.UtcDateTime,
        };
        db.Messages.Add(message);
        db.SaveChanges();
        return message;
    }

    private static SmsProviderException Transient() => new("down", isTransient: true);

    [Fact]
    public async Task Send_ProviderAccepts_MovesToSent_AndSendsOnlyNumberAndBody()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Pending, "0591231234");
        _provider.Setup(p => p.SendAsync("0591231234", "שלום", It.IsAny<CancellationToken>())).ReturnsAsync("prov-1");

        await CreateDispatcher(db).SendPendingAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.Equal("prov-1", message.ProviderMessageId);
        Assert.Null(message.FailureReason);
        _provider.Verify(p => p.SendAsync("0591231234", "שלום", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Send_TransientFailureThenSuccess_RetriesAndMovesToSent()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Pending);
        _provider.SetupSequence(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(Transient())
            .ThrowsAsync(Transient())
            .ReturnsAsync("prov-2");

        await CreateDispatcher(db).SendPendingAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Sent, message.Status);
        _provider.Verify(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Send_AllThreeAttemptsFail_MovesToFailedWithSendFailed()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Pending);
        _provider.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(Transient());

        await CreateDispatcher(db).SendPendingAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Failed, message.Status);
        Assert.Equal(FailureReasons.SendFailed, message.FailureReason);
        _provider.Verify(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Send_NonTransientFailure_DoesNotRetry()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Pending);
        _provider.Setup(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SmsProviderException("400 from provider", isTransient: false));

        await CreateDispatcher(db).SendPendingAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Failed, message.Status);
        Assert.Equal(FailureReasons.SendFailed, message.FailureReason);
        _provider.Verify(p => p.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ProviderStatus.Delivered, MessageStatus.Delivered)]
    [InlineData(ProviderStatus.Failed, MessageStatus.Failed)]
    public async Task Poll_ProviderReportsFinalStatus_UpdatesMessage(ProviderStatus providerStatus, MessageStatus expected)
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Sent, providerId: "prov-1");
        _provider.Setup(p => p.GetStatusAsync("prov-1", It.IsAny<CancellationToken>())).ReturnsAsync(providerStatus);

        await CreateDispatcher(db).PollSentAsync(CancellationToken.None);

        Assert.Equal(expected, message.Status);
    }

    [Fact]
    public async Task Poll_StillSent_BeforeTimeout_KeepsSent()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Sent, providerId: "prov-1");
        _provider.Setup(p => p.GetStatusAsync("prov-1", It.IsAny<CancellationToken>())).ReturnsAsync(ProviderStatus.Sent);
        _clock.Now = _clock.Now.AddMinutes(1);

        await CreateDispatcher(db).PollSentAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Sent, message.Status);
    }

    [Fact]
    public async Task Poll_StillSent_AfterTimeout_MovesToFailedWithDeliveryTimeout()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Sent, providerId: "prov-1");
        _provider.Setup(p => p.GetStatusAsync("prov-1", It.IsAny<CancellationToken>())).ReturnsAsync(ProviderStatus.Sent);
        _clock.Now = _clock.Now.AddMinutes(2);

        await CreateDispatcher(db).PollSentAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Failed, message.Status);
        Assert.Equal(FailureReasons.DeliveryTimeout, message.FailureReason);
    }

    [Fact]
    public async Task Poll_ProviderUnreachable_KeepsStatus()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Sent, providerId: "prov-1");
        _provider.Setup(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(Transient());

        await CreateDispatcher(db).PollSentAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Sent, message.Status);
        Assert.Null(message.FailureReason);
    }

    [Fact]
    public async Task Poll_ProviderUnreachable_AfterTimeout_MovesToDeliveryTimeout()
    {
        using var db = TestDb.Create();
        var message = AddMessage(db, MessageStatus.Sent, providerId: "prov-1");
        _provider.Setup(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(Transient());
        _clock.Now = _clock.Now.AddMinutes(3);

        await CreateDispatcher(db).PollSentAsync(CancellationToken.None);

        Assert.Equal(MessageStatus.Failed, message.Status);
        Assert.Equal(FailureReasons.DeliveryTimeout, message.FailureReason);
    }

    [Fact]
    public async Task Poll_FinalMessages_AreNotPolled()
    {
        using var db = TestDb.Create();
        AddMessage(db, MessageStatus.Delivered, providerId: "prov-1");

        await CreateDispatcher(db).PollSentAsync(CancellationToken.None);

        _provider.Verify(p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
