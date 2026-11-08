namespace SmsService.Domain;

/// <summary>Employee identified by the natural key (FirstName, LastName, Address). No external employee number exists.</summary>
public class Employee
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Address { get; set; } = "";
    public List<Phone> Phones { get; set; } = new();
}

/// <summary>A phone "instance" of an employee. The number is not unique.</summary>
public class Phone
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public string Number { get; set; } = "";
}

public enum MessageStatus
{
    Pending,
    Sent,
    Delivered,
    Failed
}

public static class FailureReasons
{
    public const string SendFailed = "SEND_FAILED";
    public const string DeliveryTimeout = "DELIVERY_TIMEOUT";
}

public class Message
{
    public Guid Id { get; set; }
    public int PhoneId { get; set; }
    public Phone Phone { get; set; } = null!;
    public string Body { get; set; } = "";
    public MessageStatus Status { get; set; } = MessageStatus.Pending;
    public string? FailureReason { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsFinal => Status is MessageStatus.Delivered or MessageStatus.Failed;
}
