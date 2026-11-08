using Microsoft.EntityFrameworkCore;
using SmsService.Domain;
using SmsService.DTO;

namespace SmsService.Application;

public class EmployeeService
{
    private readonly IAppDbContext _db;

    public EmployeeService(IAppDbContext db) => _db = db;

    /// <summary>All employees with their phones (ordered by phone Id). Employees without phones get an empty list.</summary>
    public async Task<IReadOnlyList<EmployeeDto>> ListAsync(CancellationToken ct = default)
    {
        var employees = await _db.Employees
            .AsNoTracking()
            .Include(e => e.Phones)
            .OrderBy(e => e.LastName).ThenBy(e => e.FirstName).ThenBy(e => e.Id)
            .ToListAsync(ct);

        return employees
            .Select(e => new EmployeeDto(
                e.Id, e.FirstName, e.LastName, e.Address,
                e.Phones.OrderBy(p => p.Id).Select(p => new PhoneDto(p.Id, p.Number)).ToList()))
            .ToList();
    }
}

public class MessageService
{
    public const int MaxBodyLength = 160;
    public const int MaxIdempotencyKeyLength = 100;
    public const int MaxListSize = 100;

    private readonly IAppDbContext _db;
    private readonly TimeProvider _clock;

    public MessageService(IAppDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Validates and stores a Pending message. The provider is NOT called here; the background processor sends it.
    /// If the idempotency key was already used, the existing message is returned (body is not compared, decision 45).
    /// </summary>
    public async Task<CreateMessageResponse> CreateAsync(CreateMessageRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        Validate(request, idempotencyKey);
        idempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();

        if (idempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null)
                return existing;
        }

        var phoneExists = await _db.Phones.AnyAsync(p => p.Id == request.PhoneId!.Value, ct);
        if (!phoneExists)
            throw new AppValidationException(ErrorCodes.PhoneNotFound, $"Phone with id {request.PhoneId} was not found.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var message = new Message
        {
            Id = Guid.NewGuid(),
            PhoneId = request.PhoneId!.Value,
            Body = request.Body!,
            Status = MessageStatus.Pending,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now,
            UpdatedAt = now
        };
        _db.Messages.Add(message);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (idempotencyKey is not null)
        {
            // Two concurrent requests with the same key: the unique index rejected the second one.
            _db.Messages.Remove(message);
            var existing = await FindByIdempotencyKeyAsync(idempotencyKey, ct);
            if (existing is not null)
                return existing;
            throw;
        }

        return new CreateMessageResponse(message.Id, message.Status.ToString());
    }

    public async Task<MessageDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var message = await QueryWithDetails().FirstOrDefaultAsync(m => m.Id == id, ct)
            ?? throw new AppNotFoundException(ErrorCodes.MessageNotFound, $"Message {id} was not found.");
        return ToDto(message);
    }

    /// <summary>Newest first, capped at <see cref="MaxListSize"/>, optional status filter (case-insensitive name).</summary>
    public async Task<IReadOnlyList<MessageDto>> ListAsync(string? status, CancellationToken ct = default)
    {
        var query = QueryWithDetails();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var parsed = ParseStatus(status);
            query = query.Where(m => m.Status == parsed);
        }

        var messages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(MaxListSize)
            .ToListAsync(ct);

        return messages.Select(ToDto).ToList();
    }

    public static MessageStatus ParseStatus(string status)
    {
        // Enum.TryParse also accepts numbers ("1"), so require a defined name.
        var name = Enum.GetNames<MessageStatus>()
            .FirstOrDefault(n => string.Equals(n, status.Trim(), StringComparison.OrdinalIgnoreCase));
        if (name is null)
            throw new AppValidationException(
                ErrorCodes.InvalidStatusFilter,
                $"Invalid status '{status}'. Allowed: {string.Join(", ", Enum.GetNames<MessageStatus>())}.");
        return Enum.Parse<MessageStatus>(name);
    }

    private static void Validate(CreateMessageRequest request, string? idempotencyKey)
    {
        var errors = new List<string>();

        if (request.PhoneId is null)
            errors.Add("phoneId is required.");

        if (string.IsNullOrWhiteSpace(request.Body))
            errors.Add("body is required and cannot be empty.");
        else if (request.Body.Length > MaxBodyLength)
            errors.Add($"body cannot be longer than {MaxBodyLength} characters (got {request.Body.Length}).");

        if (idempotencyKey is not null && idempotencyKey.Trim().Length > MaxIdempotencyKeyLength)
            errors.Add($"Idempotency-Key cannot be longer than {MaxIdempotencyKeyLength} characters.");

        if (errors.Count > 0)
            throw new AppValidationException(ErrorCodes.ValidationError, string.Join(" ", errors));
    }

    private async Task<CreateMessageResponse?> FindByIdempotencyKeyAsync(string key, CancellationToken ct)
    {
        var existing = await _db.Messages.AsNoTracking()
            .Where(m => m.IdempotencyKey == key)
            .Select(m => new { m.Id, m.Status })
            .FirstOrDefaultAsync(ct);
        return existing is null ? null : new CreateMessageResponse(existing.Id, existing.Status.ToString());
    }

    private IQueryable<Message> QueryWithDetails() =>
        _db.Messages.AsNoTracking().Include(m => m.Phone).ThenInclude(p => p.Employee);

    private static MessageDto ToDto(Message m) => new(
        m.Id,
        m.Status.ToString(),
        m.Body,
        m.CreatedAt,
        m.UpdatedAt,
        m.FailureReason,
        new EmployeeSummaryDto(m.Phone.Employee.FirstName, m.Phone.Employee.LastName, m.Phone.Employee.Address),
        m.Phone.Number);
}
