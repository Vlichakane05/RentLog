using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RentLog.Models;

namespace RentLog.Services.Sql;

public class DbUser { public Guid Id { get; set; } = Guid.NewGuid(); public string Email { get; set; } = ""; public string FullName { get; set; } = ""; public string PasswordHash { get; set; } = ""; public UserRole Role { get; set; } public DateTime CreatedAt { get; set; } = DateTime.UtcNow; }
public class DbPasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
public class DbProperty { public Guid Id { get; set; } = Guid.NewGuid(); public Guid LandlordId { get; set; } public string Name { get; set; } = ""; public string Address { get; set; } = ""; }
public class DbUnit { public Guid Id { get; set; } = Guid.NewGuid(); public Guid PropertyId { get; set; } public string UnitNumber { get; set; } = ""; public decimal MonthlyRent { get; set; } }
public class DbTenancy { public Guid Id { get; set; } = Guid.NewGuid(); public Guid UnitId { get; set; } public Guid TenantId { get; set; } public DateOnly StartDate { get; set; } public DateOnly? EndDate { get; set; } public bool IsActive { get; set; } = true; }

public class DbIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PropertyId { get; set; }
    public Guid UnitId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
    public string UnitNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Severity Severity { get; set; }
    public Status Status { get; set; } = Status.Open;
    public bool IsDisputed { get; set; }
    public string? ContractorName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DbIssueEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IssueId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = "";
    public string AuthorRole { get; set; } = "";
    public string EventType { get; set; } = "";
    public string Description { get; set; } = "";
    public Status? NewStatus { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DbMedia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? IssueId { get; set; }
    public Guid? EventId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileName { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long FileSizeBytes { get; set; }
    public string AltText { get; set; } = "";
    public double? DurationSeconds { get; set; }
    public byte[] Data { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DbQuote
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IssueId { get; set; }
    public string ContractorName { get; set; } = "";
    public decimal Amount { get; set; }
    public string WorkDescription { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Data { get; set; } = [];
    public string Status { get; set; } = "Pending approval";
    public Guid SubmittedBy { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}

public class DbRent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenancyId { get; set; }
    public Guid UnitId { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly Period { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? PaidDate { get; set; }
    public RentPaymentStatus Status { get; set; }
    public DateTime? LastReminderAt { get; set; }
    public int ReminderCount { get; set; }
}

public class SqlDb(DbContextOptions<SqlDb> options) : DbContext(options)
{
    public DbSet<DbUser> Users => Set<DbUser>();
    public DbSet<DbPasswordResetToken> PasswordResetTokens => Set<DbPasswordResetToken>();
    public DbSet<DbProperty> Properties => Set<DbProperty>();
    public DbSet<DbUnit> Units => Set<DbUnit>();
    public DbSet<DbTenancy> Tenancies => Set<DbTenancy>();
    public DbSet<DbIssue> Issues => Set<DbIssue>();
    public DbSet<DbIssueEvent> IssueEvents => Set<DbIssueEvent>();
    public DbSet<DbMedia> Media => Set<DbMedia>();
    public DbSet<DbQuote> Quotes => Set<DbQuote>();
    public DbSet<DbRent> Rent => Set<DbRent>();

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        c.Properties<UserRole>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<Severity>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<Status>().HaveConversion<string>().HaveMaxLength(30);
        c.Properties<RentPaymentStatus>().HaveConversion<string>().HaveMaxLength(20);
        c.Properties<decimal>().HavePrecision(12, 2);
        c.Properties<string>().HaveMaxLength(500);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        void Fk<TDep, TPrin>(Expression<Func<TDep, object?>> key) where TDep : class where TPrin : class =>
            b.Entity<TDep>().HasOne<TPrin>().WithMany().HasForeignKey(key).OnDelete(DeleteBehavior.Restrict);

        Fk<DbProperty, DbUser>(x => x.LandlordId);
        Fk<DbUnit, DbProperty>(x => x.PropertyId);
        Fk<DbTenancy, DbUnit>(x => x.UnitId);
        Fk<DbTenancy, DbUser>(x => x.TenantId);
        Fk<DbIssue, DbProperty>(x => x.PropertyId);
        Fk<DbIssue, DbUnit>(x => x.UnitId);
        Fk<DbIssue, DbUser>(x => x.TenantId);
        Fk<DbIssue, DbUser>(x => x.LandlordId);
        Fk<DbIssueEvent, DbIssue>(x => x.IssueId);
        Fk<DbIssueEvent, DbUser>(x => x.AuthorId);
        Fk<DbMedia, DbIssue>(x => x.IssueId);
        Fk<DbMedia, DbIssueEvent>(x => x.EventId);
        Fk<DbMedia, DbUser>(x => x.UploadedBy);
        Fk<DbQuote, DbIssue>(x => x.IssueId);
        Fk<DbQuote, DbUser>(x => x.SubmittedBy);
        Fk<DbRent, DbTenancy>(x => x.TenancyId);
        Fk<DbRent, DbUnit>(x => x.UnitId);
        Fk<DbRent, DbUser>(x => x.TenantId);

        b.Entity<DbUser>().Property(x => x.Email).HasMaxLength(256);
        b.Entity<DbPasswordResetToken>().Property(x => x.TokenHash).HasMaxLength(64);
        b.Entity<DbPasswordResetToken>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<DbUser>().HasIndex(x => x.Email).IsUnique();
        b.Entity<DbIssue>().Property(x => x.Title).HasMaxLength(100);
        b.Entity<DbIssue>().Property(x => x.Description).HasMaxLength(3000);
        b.Entity<DbIssue>().HasIndex(x => new { x.TenantId, x.CreatedAt });
        b.Entity<DbIssue>().HasIndex(x => new { x.LandlordId, x.CreatedAt });
        b.Entity<DbIssueEvent>().Property(x => x.Description).HasMaxLength(4000);
        b.Entity<DbIssueEvent>().HasIndex(x => new { x.IssueId, x.CreatedAt });
        b.Entity<DbQuote>().Property(x => x.WorkDescription).HasMaxLength(3000);
        b.Entity<DbTenancy>().HasIndex(x => x.UnitId).IsUnique().HasFilter("[IsActive] = 1");
        b.Entity<DbRent>().HasIndex(x => new { x.TenancyId, x.Period }).IsUnique();
        b.Entity<DbUnit>().HasIndex(x => new { x.PropertyId, x.UnitNumber }).IsUnique();
    }

    // Issue history is append-only: existing events can never be edited or deleted.
    private void GuardHistory()
    {
        if (ChangeTracker.Entries<DbIssueEvent>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Issue history is append-only.");
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { GuardHistory(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) { GuardHistory(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken); }
}
