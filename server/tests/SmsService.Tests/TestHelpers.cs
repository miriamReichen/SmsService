using Microsoft.EntityFrameworkCore;
using SmsService.Domain;
using SmsService.Infrastructure;

namespace SmsService.Tests;

/// <summary>Controllable clock for timeout tests.</summary>
public class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public static class TestDb
{
    /// <summary>New isolated EF InMemory database (does not enforce SQL Server constraints).</summary>
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static Phone AddEmployeeWithPhone(AppDbContext db, string number = "0591231234")
    {
        var employee = new Employee { FirstName = "דנה", LastName = "כהן", Address = "הרצל 2, חיפה" };
        var phone = new Phone { Number = number, Employee = employee };
        db.Employees.Add(employee);
        db.Phones.Add(phone);
        db.SaveChanges();
        return phone;
    }
}
