using Microsoft.EntityFrameworkCore;
using SmsService.Application;
using SmsService.Domain;

namespace SmsService.Infrastructure;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Phone> Phones => Set<Phone>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(e =>
        {
            e.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            e.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Address).HasMaxLength(200).IsRequired();
            // The natural key from the exam: first name + last name + address are unique together.
            e.HasIndex(x => new { x.FirstName, x.LastName, x.Address }).IsUnique();
        });

        modelBuilder.Entity<Phone>(p =>
        {
            p.Property(x => x.Number).HasMaxLength(20).IsRequired();
            p.HasIndex(x => x.Number); // NOT unique: the same number may belong to several instances.
            p.HasOne(x => x.Employee).WithMany(x => x.Phones)
                .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(m =>
        {
            m.Property(x => x.Body).HasMaxLength(MessageService.MaxBodyLength).IsRequired();
            m.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            m.Property(x => x.FailureReason).HasMaxLength(50);
            m.Property(x => x.ProviderMessageId).HasMaxLength(100);
            m.Property(x => x.IdempotencyKey).HasMaxLength(MessageService.MaxIdempotencyKeyLength);
            m.HasIndex(x => x.Status);
            m.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            m.HasOne(x => x.Phone).WithMany()
                .HasForeignKey(x => x.PhoneId).OnDelete(DeleteBehavior.Restrict);
            m.Ignore(x => x.IsFinal);
        });
    }
}
