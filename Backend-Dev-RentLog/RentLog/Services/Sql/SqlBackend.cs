using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentLog.Models;

namespace RentLog.Services.Sql;

public static class SqlServerBackend
{
    public static IServiceCollection AddSqlServerBackend(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<SqlDb>(o => o.UseSqlServer(connectionString, s => s.EnableRetryOnFailure()));
        services.AddScoped<IAuthService, SqlAuthService>();
        services.AddScoped<IIssueService, SqlIssueService>();
        services.AddScoped<IRentService, SqlRentService>();
        services.AddScoped<IPropertyService, SqlPropertyService>();
        services.AddScoped<ITenancyService, SqlTenancyService>();
        services.AddScoped<IMediaService, SqlMediaService>();
        return services;
    }

    public static async Task UseSqlServerBackendAsync(this WebApplication app)
    {
        var factory = app.Services.GetRequiredService<IDbContextFactory<SqlDb>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            // EnsureCreated does not add tables to an existing database, so create this feature's
            // token table idempotently for installations created before password recovery existed.
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.PasswordResetTokens', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.PasswordResetTokens (
                        Id uniqueidentifier NOT NULL CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
                        UserId uniqueidentifier NOT NULL,
                        TokenHash nvarchar(64) NOT NULL,
                        ExpiresAt datetime2 NOT NULL,
                        UsedAt datetime2 NULL,
                        CreatedAt datetime2 NOT NULL,
                        CONSTRAINT FK_PasswordResetTokens_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users(Id) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IX_PasswordResetTokens_TokenHash ON dbo.PasswordResetTokens(TokenHash);
                    CREATE INDEX IX_PasswordResetTokens_UserId ON dbo.PasswordResetTokens(UserId);
                END
                """);
            if (app.Environment.IsDevelopment() && !await db.Users.AnyAsync()) await SeedAsync(db);
        }

        // Evidence files are served by unguessable GUID links, because sign-in lives in the Blazor circuit, not a cookie.
        app.MapGet("/sqlmedia/{id:guid}", async (Guid id, HttpContext ctx, IDbContextFactory<SqlDb> f) =>
        {
            await using var db = await f.CreateDbContextAsync();
            var m = await db.Media.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Data, x.MimeType }).FirstOrDefaultAsync();
            if (m is null) return Results.NotFound();
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["Cache-Control"] = "private, max-age=3600";
            return Results.File(new MemoryStream(m.Data), m.MimeType, enableRangeProcessing: true);
        });
    }

    // Development-only demo data that mirrors the mock services. Demo password: password123
    private static async Task SeedAsync(SqlDb db)
    {
        var hasher = new PasswordHasher<DbUser>();
        DbUser U(string email, string name, UserRole role)
        {
            var u = new DbUser { Email = email, FullName = name, Role = role };
            u.PasswordHash = hasher.HashPassword(u, "password123");
            return u;
        }
        var landlord = U("landlord@example.com", "Jane Landlord", UserRole.Landlord);
        var john = U("tenant@example.com", "John Tenant", UserRole.Tenant);
        var alex = U("alex@example.com", "Alex Tenant", UserRole.Tenant);
        var sam = U("sam@example.com", "Sam Tenant", UserRole.Tenant);
        var morgan = U("morgan@example.com", "Morgan Tenant", UserRole.Tenant);
        db.Users.AddRange(landlord, john, alex, sam, morgan);

        var cape = new DbProperty { LandlordId = landlord.Id, Name = "Cape Gardens", Address = "14 Rose Street, Gardens, Cape Town" };
        var oak = new DbProperty { LandlordId = landlord.Id, Name = "Oak Court", Address = "82 Main Road, Rondebosch, Cape Town" };
        db.Properties.AddRange(cape, oak);

        DbUnit Un(DbProperty p, string n, decimal rent) => new() { PropertyId = p.Id, UnitNumber = n, MonthlyRent = rent };
        var u2a = Un(cape, "2A", 9200); var u2b = Un(cape, "2B", 8500); var u3b = Un(cape, "3B", 8500);
        var u1c = Un(oak, "1C", 7800); var u4d = Un(oak, "4D", 8500);
        db.Units.AddRange(u2a, u2b, u3b, u1c, u4d);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var first = new DateOnly(today.Year, today.Month, 1);
        foreach (var (unit, tenant) in new[] { (u2a, alex), (u3b, john), (u1c, sam), (u4d, morgan) })
        {
            var tenancy = new DbTenancy { UnitId = unit.Id, TenantId = tenant.Id, StartDate = first.AddMonths(-6) };
            db.Tenancies.Add(tenancy);
            for (var m = 0; m < 3; m++)
            {
                var period = first.AddMonths(-m);
                var due = period.AddDays(14);
                var overdue = m == 0 ? today > due : tenant == sam && m == 1;
                var paid = m > 0 && !overdue;
                db.Rent.Add(new DbRent
                {
                    TenancyId = tenancy.Id, UnitId = unit.Id, TenantId = tenant.Id, Period = period, Amount = unit.MonthlyRent, DueDate = due,
                    PaidDate = paid ? period.AddDays(2) : null,
                    Status = paid ? RentPaymentStatus.Paid : overdue ? RentPaymentStatus.Overdue : RentPaymentStatus.Due
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
