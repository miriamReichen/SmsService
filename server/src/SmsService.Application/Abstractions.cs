using Microsoft.EntityFrameworkCore;
using SmsService.Domain;

namespace SmsService.Application;

/// <summary>Persistence abstraction. Implemented by Infrastructure (SQL Server), and by EF InMemory in tests.</summary>
public interface IAppDbContext
{
    DbSet<Employee> Employees { get; }
    DbSet<Phone> Phones { get; }
    DbSet<Message> Messages { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Status values reported by the SMS provider.</summary>
public enum ProviderStatus
{
    Sent,
    Delivered,
    Failed
}

/// <summary>
/// Thrown by <see cref="ISmsProviderClient"/> when a call fails.
/// <see cref="IsTransient"/> is true for network errors, timeouts and 5xx (worth retrying).
/// </summary>
public class SmsProviderException : Exception
{
    public bool IsTransient { get; }

    public SmsProviderException(string message, bool isTransient, Exception? inner = null)
        : base(message, inner)
    {
        IsTransient = isTransient;
    }
}

/// <summary>
/// Contract with the SMS provider. Only the phone number and body leave our system.
/// A second provider would be another implementation of this interface.
/// </summary>
public interface ISmsProviderClient
{
    /// <returns>The provider's message id.</returns>
    Task<string> SendAsync(string to, string body, CancellationToken cancellationToken);

    Task<ProviderStatus> GetStatusAsync(string providerMessageId, CancellationToken cancellationToken);
}

public class MessageProcessingOptions
{
    public const string SectionName = "MessageProcessing";

    public int PollIntervalSeconds { get; set; } = 2;
    public int DeliveryTimeoutMinutes { get; set; } = 2;
    public int MaxSendAttempts { get; set; } = 3;

    /// <summary>Delay before attempt 2, 3, ... (seconds). Defaults to 1, 2, 4 (growing).</summary>
    /// <remarks>Null default (not an initialized array) because the config binder appends to existing arrays.</remarks>
    public int[]? RetryDelaysSeconds { get; set; }

    public static readonly int[] DefaultRetryDelaysSeconds = { 1, 2, 4 };
}
