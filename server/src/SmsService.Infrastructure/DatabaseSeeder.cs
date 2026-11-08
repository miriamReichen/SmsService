using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmsService.Application;
using SmsService.Domain;

namespace SmsService.Infrastructure;

/// <summary>Seeds sample employees only when the Employees table is empty. Invalid phone numbers are skipped.</summary>
public static class DatabaseSeeder
{
    private record SeedEmployee(string FirstName, string LastName, string Address, params string[] Phones);

    private static readonly SeedEmployee[] Data =
    {
        new("דנה", "כהן", "הרצל 2, חיפה", "0591231234", "0591239874"), // two phones
        new("יוסי", "לוי", "ביאליק 7, תל אביב", "0521112222"),           // one phone
        new("מיכל", "אברהם", "הנביאים 12, ירושלים", "0541230000"),       // ends with 0000 -> provider fails it
        new("רון", "ישראלי", "העצמאות 3, באר שבע"),                      // no phones
        new("נועה", "מזרחי", "רוטשילד 40, ראשון לציון", "0501234567", "12345"), // "12345" is invalid -> skipped
    };

    public static async Task SeedAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        if (await db.Employees.AnyAsync(ct))
            return;

        foreach (var seed in Data)
        {
            var employee = new Employee { FirstName = seed.FirstName, LastName = seed.LastName, Address = seed.Address };
            foreach (var number in seed.Phones)
            {
                if (PhoneNumberValidator.IsValid(number))
                    employee.Phones.Add(new Phone { Number = number });
                else
                    logger.LogWarning("Seed: skipped invalid phone number '{Number}' for {First} {Last}",
                        number, seed.FirstName, seed.LastName);
            }
            db.Employees.Add(employee);
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed: inserted {Count} employees", Data.Length);
    }
}
