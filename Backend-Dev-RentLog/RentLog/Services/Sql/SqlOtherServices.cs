using Microsoft.EntityFrameworkCore;
using RentLog.Models;

namespace RentLog.Services.Sql;

public sealed class SqlRentService(IDbContextFactory<SqlDb> factory) : IRentService
{
    public async Task<IReadOnlyList<RentEntry>> GetRentHistoryAsync(string tenantId)
    {
        if (!Guid.TryParse(tenantId, out var id)) return [];
        await using var db = await factory.CreateDbContextAsync();
        await MarkOverdueAsync(db);
        var rows = await db.Rent.AsNoTracking().Where(r => r.TenantId == id).OrderByDescending(r => r.Period).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<RentEntry>> GetLandlordRentEntriesAsync(string landlordId)
    {
        if (!Guid.TryParse(landlordId, out var id)) return [];
        await using var db = await factory.CreateDbContextAsync();
        await MarkOverdueAsync(db);
        var rows = await (from r in db.Rent.AsNoTracking()
                          join u in db.Units on r.UnitId equals u.Id
                          join p in db.Properties on u.PropertyId equals p.Id
                          where p.LandlordId == id
                          orderby r.Period descending, r.TenantId
                          select r).ToListAsync();
        return rows.Select(Map).ToList();
    }

    public async Task<bool> SendReminderAsync(AuthUser landlord, string rentEntryId)
    {
        ArgumentNullException.ThrowIfNull(landlord);
        if (landlord.Role != UserRole.Landlord) throw new InvalidOperationException("Only the assigned landlord can send rent reminders.");
        if (!Guid.TryParse(rentEntryId, out var rid) || !Guid.TryParse(landlord.Id, out var lid)) return false;
        await using var db = await factory.CreateDbContextAsync();
        var entry = await (from r in db.Rent
                           join u in db.Units on r.UnitId equals u.Id
                           join p in db.Properties on u.PropertyId equals p.Id
                           where r.Id == rid && p.LandlordId == lid
                           select r).FirstOrDefaultAsync();
        if (entry is null || entry.Status == RentPaymentStatus.Paid) return false;
        entry.LastReminderAt = DateTime.UtcNow; // persisted only; no email/SMS provider is configured
        entry.ReminderCount++;
        await db.SaveChangesAsync();
        return true;
    }

    private static Task<int> MarkOverdueAsync(SqlDb db)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return db.Rent.Where(r => r.Status == RentPaymentStatus.Due && r.DueDate < today)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RentPaymentStatus.Overdue));
    }

    private static RentEntry Map(DbRent r) => new()
    {
        Id = r.Id.ToString(), TenantId = r.TenantId.ToString(), Period = r.Period, Amount = r.Amount, DueDate = r.DueDate,
        PaidDate = r.PaidDate, Status = r.Status,
        LastReminderAt = r.LastReminderAt is null ? null : DateTime.SpecifyKind(r.LastReminderAt.Value, DateTimeKind.Utc),
        ReminderCount = r.ReminderCount
    };
}

public sealed class SqlPropertyService(IDbContextFactory<SqlDb> factory) : IPropertyService
{
    public async Task<IReadOnlyList<RentalProperty>> GetPropertiesAsync(string landlordId)
    {
        if (!Guid.TryParse(landlordId, out var id)) return [];
        await using var db = await factory.CreateDbContextAsync();
        var props = await db.Properties.AsNoTracking().Where(p => p.LandlordId == id).OrderBy(p => p.Name).ToListAsync();
        var pids = props.Select(p => p.Id).ToList();
        var units = await db.Units.AsNoTracking().Where(u => pids.Contains(u.PropertyId)).OrderBy(u => u.UnitNumber).ToListAsync();
        var uids = units.Select(u => u.Id).ToList();
        var tenants = await (from t in db.Tenancies.AsNoTracking()
                             where t.IsActive && uids.Contains(t.UnitId)
                             join u in db.Users on t.TenantId equals u.Id
                             select new { t.UnitId, TenantId = u.Id, u.FullName }).ToListAsync();
        return props.Select(p => new RentalProperty
        {
            Id = p.Id.ToString(), Name = p.Name, Address = p.Address,
            Units = units.Where(u => u.PropertyId == p.Id).Select(u =>
            {
                var tenant = tenants.FirstOrDefault(t => t.UnitId == u.Id);
                return new RentalUnit
                {
                    Id = u.Id.ToString(), UnitNumber = u.UnitNumber, MonthlyRent = u.MonthlyRent, IsOccupied = tenant is not null,
                    TenantName = tenant?.FullName ?? "", TenantId = tenant?.TenantId.ToString() ?? ""
                };
            }).ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<ContractorJob>> GetContractorJobsAsync(string landlordId)
    {
        if (!Guid.TryParse(landlordId, out var id)) return [];
        await using var db = await factory.CreateDbContextAsync();
        var rows = await (from i in db.Issues.AsNoTracking()
                          where i.LandlordId == id && i.ContractorName != null &&
                                (i.Status == Status.Acknowledged || i.Status == Status.InProgress || i.Status == Status.AwaitingConfirmation)
                          join p in db.Properties on i.PropertyId equals p.Id
                          orderby i.CreatedAt descending
                          select new { Issue = i, PropertyName = p.Name }).ToListAsync();
        return rows.Select(r => new ContractorJob
        {
            Id = $"job-{r.Issue.Id}", IssueId = r.Issue.Id.ToString(), Title = r.Issue.Title, PropertyName = r.PropertyName,
            UnitNumber = r.Issue.UnitNumber, ContractorName = r.Issue.ContractorName!, ScheduledDate = null,
            Status = r.Issue.Status, Severity = r.Issue.Severity
        }).ToList();
    }
}

public sealed class SqlTenancyService(IDbContextFactory<SqlDb> factory) : ITenancyService
{
    public async Task<IReadOnlyList<TenantUnitOption>> GetTenantUnitsAsync(string tenantId)
    {
        if (!Guid.TryParse(tenantId, out var id)) return [];
        await using var db = await factory.CreateDbContextAsync();
        var rows = await (from t in db.Tenancies.AsNoTracking()
                          where t.TenantId == id && t.IsActive
                          join u in db.Units on t.UnitId equals u.Id
                          join p in db.Properties on u.PropertyId equals p.Id
                          orderby p.Name, u.UnitNumber
                          select new { u.Id, u.UnitNumber, PropertyName = p.Name }).ToListAsync();
        return rows.Select(r => new TenantUnitOption { Id = r.Id.ToString(), UnitNumber = r.UnitNumber, PropertyName = r.PropertyName }).ToList();
    }
}

public sealed class SqlMediaService(IDbContextFactory<SqlDb> factory, IAuthService auth) : IMediaService
{
    private static readonly string[] PhotoTypes = ["image/jpeg", "image/png", "image/heic"];
    private static readonly string[] VideoTypes = ["video/mp4", "video/quicktime"];
    public long MaxPhotoSizeBytes => 10 * 1024 * 1024;
    public long MaxVideoSizeBytes => 50 * 1024 * 1024;
    public double MaxVideoDurationSeconds => 30.0;

    public string? ValidatePhoto(string mimeType, long fileSizeBytes)
    {
        if (string.IsNullOrEmpty(mimeType)) return "File type is required.";
        if (!PhotoTypes.Contains(mimeType.ToLowerInvariant())) return "Invalid photo type. Accepted: JPG, PNG, HEIC.";
        if (fileSizeBytes == 0) return "File is empty.";
        if (fileSizeBytes > MaxPhotoSizeBytes) return "Photo too large. Max 10 MB.";
        return null;
    }

    public string? ValidateVideo(string mimeType, long fileSizeBytes, double? durationSeconds)
    {
        if (string.IsNullOrEmpty(mimeType)) return "File type is required.";
        if (!VideoTypes.Contains(mimeType.ToLowerInvariant())) return "Invalid video type. Accepted: MP4, MOV.";
        if (fileSizeBytes == 0) return "File is empty.";
        if (fileSizeBytes > MaxVideoSizeBytes) return "Video too large. Max 50 MB.";
        if (durationSeconds > MaxVideoDurationSeconds) return "Video too long. Max 30 seconds.";
        return null;
    }

    public async Task<string?> UploadAsync(MediaFile media, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var user = auth.CurrentUser ?? throw new InvalidOperationException("Sign in before uploading evidence.");
        var raw = media.Base64Data ?? "";
        var comma = raw.IndexOf(',');
        if (raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0) raw = raw[(comma + 1)..];
        byte[] data;
        try { data = Convert.FromBase64String(raw); } catch (FormatException) { return null; }

        var mime = (media.MimeType ?? "").ToLowerInvariant();
        var error = mime.StartsWith("image/") ? ValidatePhoto(mime, data.Length) : ValidateVideo(mime, data.Length, media.DurationSeconds);
        if (error is not null) return null;

        progress?.Report(50);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var row = new DbMedia
        {
            UploadedBy = Guid.Parse(user.Id), FileName = Path.GetFileName(media.FileName), MimeType = mime,
            FileSizeBytes = data.Length, AltText = media.AltText ?? "", DurationSeconds = media.DurationSeconds, Data = data
        };
        db.Media.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        progress?.Report(100);
        return $"/sqlmedia/{row.Id}";
    }
}
