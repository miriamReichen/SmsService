using SmsService.Application;
using SmsService.Domain;
using SmsService.DTO;

namespace SmsService.Tests;

public class MessageServiceTests
{
    private readonly FakeClock _clock = new();

    [Fact]
    public async Task Create_ValidRequest_SavesPendingMessage()
    {
        using var db = TestDb.Create();
        var phone = TestDb.AddEmployeeWithPhone(db);
        var service = new MessageService(db, _clock);

        var result = await service.CreateAsync(new CreateMessageRequest { PhoneId = phone.Id, Body = "שלום" }, null);

        Assert.Equal("Pending", result.Status);
        var saved = Assert.Single(db.Messages);
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal(MessageStatus.Pending, saved.Status);
        Assert.Equal(phone.Id, saved.PhoneId);
    }

    [Fact]
    public async Task Create_UnknownPhone_Throws400PhoneNotFound_AndSavesNothing()
    {
        using var db = TestDb.Create();
        TestDb.AddEmployeeWithPhone(db);
        var service = new MessageService(db, _clock);

        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(new CreateMessageRequest { PhoneId = 9999, Body = "hi" }, null));

        Assert.Equal(ErrorCodes.PhoneNotFound, ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Empty(db.Messages);
    }

    [Theory]
    [InlineData(160, true)]
    [InlineData(161, false)]
    public async Task Create_BodyLengthLimit_Is160(int length, bool shouldPass)
    {
        using var db = TestDb.Create();
        var phone = TestDb.AddEmployeeWithPhone(db);
        var service = new MessageService(db, _clock);
        var request = new CreateMessageRequest { PhoneId = phone.Id, Body = new string('א', length) };

        if (shouldPass)
        {
            await service.CreateAsync(request, null);
            Assert.Single(db.Messages);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<AppValidationException>(() => service.CreateAsync(request, null));
            Assert.Equal(ErrorCodes.ValidationError, ex.Code);
            Assert.Empty(db.Messages);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_MissingOrEmptyBody_ThrowsValidationError(string? body)
    {
        using var db = TestDb.Create();
        var phone = TestDb.AddEmployeeWithPhone(db);
        var service = new MessageService(db, _clock);

        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(new CreateMessageRequest { PhoneId = phone.Id, Body = body }, null));

        Assert.Equal(ErrorCodes.ValidationError, ex.Code);
    }

    [Fact]
    public async Task Create_MissingPhoneId_ThrowsValidationError()
    {
        using var db = TestDb.Create();
        var service = new MessageService(db, _clock);

        var ex = await Assert.ThrowsAsync<AppValidationException>(() =>
            service.CreateAsync(new CreateMessageRequest { PhoneId = null, Body = "hi" }, null));

        Assert.Equal(ErrorCodes.ValidationError, ex.Code);
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("1")] // numeric values must not be accepted as a status
    public async Task List_InvalidStatus_ThrowsInvalidStatusFilter(string status)
    {
        using var db = TestDb.Create();
        var service = new MessageService(db, _clock);

        var ex = await Assert.ThrowsAsync<AppValidationException>(() => service.ListAsync(status));

        Assert.Equal(ErrorCodes.InvalidStatusFilter, ex.Code);
    }

    [Fact]
    public async Task Get_UnknownId_Throws404()
    {
        using var db = TestDb.Create();
        var service = new MessageService(db, _clock);

        var ex = await Assert.ThrowsAsync<AppNotFoundException>(() => service.GetAsync(Guid.NewGuid()));

        Assert.Equal(404, ex.StatusCode);
    }
}
