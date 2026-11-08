namespace SmsService.DTO;

public record PhoneDto(int PhoneId, string Number);

public record EmployeeDto(int Id, string FirstName, string LastName, string Address, IReadOnlyList<PhoneDto> Phones);

public record EmployeeSummaryDto(string FirstName, string LastName, string Address);

/// <summary>Request body for POST /messages. Nullable so missing fields reach our own validation.</summary>
public class CreateMessageRequest
{
    public int? PhoneId { get; set; }
    public string? Body { get; set; }
}

public record CreateMessageResponse(Guid Id, string Status);

public record MessageDto(
    Guid Id,
    string Status,
    string Body,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? FailureReason,
    EmployeeSummaryDto Employee,
    string PhoneNumber);

public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string PhoneNotFound = "PHONE_NOT_FOUND";
    public const string InvalidStatusFilter = "INVALID_STATUS_FILTER";
    public const string MessageNotFound = "MESSAGE_NOT_FOUND";
}
