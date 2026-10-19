using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RentLog.Models;

namespace RentLog.Services.Sql;

public sealed class SqlIssueService(IDbContextFactory<SqlDb> factory) : IIssueService
{
    public async Task<IReadOnlyList<Issue>> GetTenantIssuesAsync(string tenantId)
    {
        if (!Guid.TryParse(tenantId, out var id)) return [];
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.Issues.AsNoTracking().Where(i => i.TenantId == id).OrderByDescending(i => i.CreatedAt).ToListAsync();
        return await MapAsync(db, rows);
    }

    public async Task<IReadOnlyList<Issue>> GetLandlordIssuesAsync(string landlordId)
    {
        if (!Guid.TryParse(landlordId, out var id)) return [];
        await using var db = await factory.CreateDbContextAsync();
        var rows = await db.Issues.AsNoTracking().Where(i => i.LandlordId == id).OrderByDescending(i => i.CreatedAt).ToListAsync();
        return await MapAsync(db, rows);
    }

    public async Task<Issue?> GetTenantIssueAsync(string tenantId, string issueId)
    {
        if (!Guid.TryParse(tenantId, out var tid) || !Guid.TryParse(issueId, out var iid)) return null;
        await using var db = await factory.CreateDbContextAsync();
        return await db.Issues.AnyAsync(i => i.Id == iid && i.TenantId == tid) ? await DetailAsync(db, iid) : null;
    }

    public async Task<Issue?> GetLandlordIssueAsync(string landlordId, string issueId)
    {
        if (!Guid.TryParse(landlordId, out var lid) || !Guid.TryParse(issueId, out var iid)) return null;
        await using var db = await factory.CreateDbContextAsync();
        return await db.Issues.AnyAsync(i => i.Id == iid && i.LandlordId == lid) ? await DetailAsync(db, iid) : null;
    }

    public async Task<Issue> CreateIssueAsync(AuthUser tenant, string unit, string title, string description, Severity severity, IReadOnlyList<string> mediaUrls)
    {
        EnsureRole(tenant, UserRole.Tenant);
        if (!Guid.TryParse(tenant.Id, out var tenantId))
            throw new InvalidOperationException("Your account could not be verified. Sign in again and retry.");
        title = (title ?? string.Empty).Trim();
        description = (description ?? string.Empty).Trim();
        if (title.Length is 0 or > 100) throw new InvalidOperationException("The title must be between 1 and 100 characters.");
        if (description.Length is < 10 or > 3000) throw new InvalidOperationException("The description must be between 10 and 3000 characters.");
        if (!Enum.IsDefined(severity)) throw new InvalidOperationException("Choose a valid issue severity.");
        if (string.IsNullOrWhiteSpace(unit)) throw new InvalidOperationException("Choose one of your active rental units.");

        await using var db = await factory.CreateDbContextAsync();
        var mediaIds = mediaUrls.Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => Guid.TryParse(u.Split('/').Last(), out var g) ? g : throw new InvalidOperationException("Invalid evidence upload."))
            .Distinct().ToList();
        var files = await db.Media.Where(x => mediaIds.Contains(x.Id) && x.UploadedBy == tenantId && x.IssueId == null).ToListAsync();
        if (files.Count != mediaIds.Count) throw new InvalidOperationException("The evidence upload details expired. Upload the files again before submitting the issue.");
        if (files.Count(x => x.MimeType.StartsWith("image/")) > 5 || files.Count(x => x.MimeType.StartsWith("video/")) > 2)
            throw new InvalidOperationException("An issue can include up to 5 photos and 2 videos.");

        var wanted = (unit ?? "").Trim();
        var candidates = await (from t in db.Tenancies
                                where t.TenantId == tenantId && t.IsActive
                                join u in db.Units on t.UnitId equals u.Id
                                join p in db.Properties on u.PropertyId equals p.Id
                                select new { Unit = u, Property = p }).ToListAsync();
        var matches = candidates.Where(c => Guid.TryParse(wanted, out var g)
            ? c.Unit.Id == g
            : string.Equals(c.Unit.UnitNumber, wanted, StringComparison.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0) throw new InvalidOperationException("The selected unit is not part of your active tenancy.");
        if (matches.Count > 1) throw new InvalidOperationException("The selected unit is ambiguous. Choose one of your active units and try again.");

        var match = matches[0];
        var issue = new DbIssue
        {
            PropertyId = match.Property.Id, UnitId = match.Unit.Id, TenantId = tenantId, LandlordId = match.Property.LandlordId,
            UnitNumber = match.Unit.UnitNumber, Title = title, Description = description, Severity = severity, Status = Status.Open
        };
        db.Issues.Add(issue);
        var eventId = AddEvent(db, issue.Id, tenant, "StatusChange", description, Status.Open);
        foreach (var file in files) { file.IssueId = issue.Id; file.EventId = eventId; }
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id) ?? throw new InvalidOperationException("The new issue could not be loaded after it was saved.");
    }

    public async Task<Issue?> AddCommentAsync(AuthUser author, string issueId, string comment)
    {
        comment = (comment ?? string.Empty).Trim();
        if (comment.Length is 0 or > 4000) throw new InvalidOperationException("A comment must be between 1 and 4000 characters.");
        if (author is null || !Enum.IsDefined(author.Role)) throw new InvalidOperationException("Your account could not be verified. Sign in again and retry.");
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, author, issueId);
        if (issue is null) return null;
        AddEvent(db, issue.Id, author, "Comment", comment);
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id);
    }

    public async Task<Issue?> ConfirmResolutionAsync(AuthUser tenant, string issueId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, tenant, issueId, UserRole.Tenant);
        if (issue is null || issue.Status != Status.AwaitingConfirmation) return null;
        Transition(db, issue, tenant, Status.Resolved);
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id);
    }

    public async Task<Issue?> DisputeResolutionAsync(AuthUser tenant, string issueId, string reason)
    {
        EnsureRole(tenant, UserRole.Tenant);
        reason = (reason ?? string.Empty).Trim();
        if (reason.Length is < 2 or > 2000)
            throw new InvalidOperationException("Explain what still needs attention in 2 to 2,000 characters.");
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, tenant, issueId, UserRole.Tenant);
        if (issue is null || issue.Status != Status.AwaitingConfirmation) return null;
        Transition(db, issue, tenant, Status.Disputed);
        AddEvent(db, issue.Id, tenant, "Comment", reason);
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id);
    }

    public async Task<Issue?> AcknowledgeAsync(AuthUser landlord, string issueId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, landlord, issueId, UserRole.Landlord);
        if (issue is null || issue.Status != Status.Open) return null;
        Transition(db, issue, landlord, Status.Acknowledged);
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id);
    }

    public async Task<IssueQuote?> AddQuoteAsync(AuthUser landlord, string issueId, string contractorName, decimal amount, string workDescription, string fileName, string fileDataUrl)
    {
        EnsureRole(landlord, UserRole.Landlord);
        contractorName = (contractorName ?? string.Empty).Trim();
        workDescription = (workDescription ?? string.Empty).Trim();
        fileName = Path.GetFileName(fileName ?? string.Empty);
        if (contractorName.Length is < 2 or > 120)
            throw new InvalidOperationException("Enter a contractor name between 2 and 120 characters.");
        if (workDescription.Length is < 2 or > 1000)
            throw new InvalidOperationException("Describe the quoted work in 2 to 1,000 characters.");
        if (fileName.Length is 0 or > 255)
            throw new InvalidOperationException("Choose a valid quote filename.");
        if (amount <= 0 || amount > 100_000_000m)
            throw new InvalidOperationException("The quote amount must be greater than R0 and no more than R100,000,000.");
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, landlord, issueId, UserRole.Landlord);
        if (issue is null || issue.Status is not (Status.Acknowledged or Status.InProgress)) return null;

        var (bytes, type) = DecodeDataUrl(fileDataUrl);
        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024) throw new InvalidOperationException("The contractor quote must be larger than 0 bytes and no more than 10 MB.");
        if (type is not ("application/pdf" or "image/jpeg" or "image/png")) throw new InvalidOperationException("Contractor quotes must be PDF, JPG or PNG files.");

        var quote = new DbQuote
        {
            IssueId = issue.Id, ContractorName = contractorName.Trim(), Amount = amount, WorkDescription = workDescription.Trim(),
            FileName = Path.GetFileName(fileName), ContentType = type, Data = bytes, SubmittedBy = Guid.Parse(landlord.Id)
        };
        db.Quotes.Add(quote);
        AddEvent(db, issue.Id, landlord, "Quote", $"Quote from {quote.ContractorName} for R {Money(amount)}: {quote.WorkDescription} ({quote.FileName})");
        await db.SaveChangesAsync();
        return new IssueQuote
        {
            Id = quote.Id.ToString(), ContractorName = quote.ContractorName, Amount = amount, WorkDescription = quote.WorkDescription,
            FileName = quote.FileName, FileDataUrl = fileDataUrl, Status = quote.Status, SubmittedBy = landlord.Name,
            SubmittedAt = DateTime.SpecifyKind(quote.SubmittedAt, DateTimeKind.Utc)
        };
    }

    public async Task<Issue?> ApproveQuoteAsync(AuthUser landlord, string issueId, string quoteId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, landlord, issueId, UserRole.Landlord);
        if (issue is null || issue.Status is not (Status.Acknowledged or Status.InProgress) || !Guid.TryParse(quoteId, out var qid)) return null;
        var quote = await db.Quotes.FirstOrDefaultAsync(q => q.Id == qid && q.IssueId == issue.Id);
        if (quote is null || quote.Status != "Pending approval") return null;

        quote.Status = "Approved";
        issue.ContractorName = quote.ContractorName;
        AddEvent(db, issue.Id, landlord, "Quote", $"The quote from {quote.ContractorName} for R {Money(quote.Amount)} was approved.");
        if (issue.Status == Status.Acknowledged) Transition(db, issue, landlord, Status.InProgress);
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id);
    }

    public async Task<Issue?> MarkFixedAsync(AuthUser landlord, string issueId, string comment)
    {
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, landlord, issueId, UserRole.Landlord);
        if (issue is null || issue.Status is not (Status.Acknowledged or Status.InProgress)) return null;
        comment = (comment ?? string.Empty).Trim();
        if (comment.Length is < 2 or > 2000)
            throw new InvalidOperationException("Add repair details between 2 and 2,000 characters before marking the issue fixed.");
        AddEvent(db, issue.Id, landlord, "Comment", comment);
        Transition(db, issue, landlord, Status.AwaitingConfirmation);
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id);
    }

    public async Task<Issue?> RespondToDisputeAsync(AuthUser landlord, string issueId, string response, bool agreeResolved)
    {
        EnsureRole(landlord, UserRole.Landlord);
        response = (response ?? string.Empty).Trim();
        if (response.Length is < 2 or > 2000)
            throw new InvalidOperationException("Write a response between 2 and 2,000 characters.");
        await using var db = await factory.CreateDbContextAsync();
        var issue = await FindAsync(db, landlord, issueId, UserRole.Landlord);
        if (issue is null || issue.Status != Status.Disputed) return null;
        AddEvent(db, issue.Id, landlord, "Comment", response);
        Transition(db, issue, landlord, agreeResolved ? Status.AwaitingConfirmation : Status.InProgress);
        await db.SaveChangesAsync();
        return await DetailAsync(db, issue.Id);
    }

    // ---- helpers ----
    private static string Money(decimal amount) => amount.ToString("N2", CultureInfo.InvariantCulture);

    private static void EnsureRole(AuthUser user, UserRole role)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Role != role) throw new InvalidOperationException($"Only a {role.ToString().ToLowerInvariant()} can perform this issue action.");
    }

    // Returns the tracked issue only if the signed-in user is its tenant (or its landlord).
    private static async Task<DbIssue?> FindAsync(SqlDb db, AuthUser user, string issueId, UserRole? role = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (role is not null) EnsureRole(user, role.Value);
        if (user.Role is not (UserRole.Tenant or UserRole.Landlord))
            throw new InvalidOperationException("Your account does not have permission to update maintenance issues.");
        if (!Guid.TryParse(issueId, out var iid) || !Guid.TryParse(user.Id, out var uid)) return null;
        var query = db.Issues.Where(i => i.Id == iid);
        query = user.Role == UserRole.Tenant ? query.Where(i => i.TenantId == uid) : query.Where(i => i.LandlordId == uid);
        return await query.FirstOrDefaultAsync();
    }

    private static Guid AddEvent(SqlDb db, Guid issueId, AuthUser actor, string type, string text, Status? newStatus = null)
    {
        var e = new DbIssueEvent
        {
            IssueId = issueId, AuthorId = Guid.Parse(actor.Id), AuthorName = actor.Name, AuthorRole = actor.Role.ToString(),
            EventType = type, Description = (text ?? "").Trim(), NewStatus = newStatus
        };
        db.IssueEvents.Add(e);
        return e.Id;
    }

    private static void Transition(SqlDb db, DbIssue issue, AuthUser actor, Status next)
    {
        issue.Status = next;
        issue.IsDisputed = next == Status.Disputed;
        AddEvent(db, issue.Id, actor, "StatusChange", next switch
        {
            Status.Acknowledged => "The landlord acknowledged this issue.",
            Status.InProgress => "Work is continuing on this issue.",
            Status.AwaitingConfirmation => "The landlord marked this issue fixed. Please confirm the repair.",
            Status.Resolved => "The tenant confirmed the issue is resolved.",
            Status.Disputed => "The tenant disputed the resolution.",
            _ => "The issue status changed."
        }, next);
    }

    private static (byte[] Bytes, string Type) DecodeDataUrl(string dataUrl)
    {
        var comma = dataUrl?.IndexOf(',') ?? -1;
        if (dataUrl is null || !dataUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || comma < 0)
            throw new InvalidDataException("The quote attachment is not a valid data URL.");
        var header = dataUrl[5..comma];
        var type = header.Split(';')[0].ToLowerInvariant();
        if (type.Length == 0 || !header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The quote attachment does not include a valid content type.");
        try { return (Convert.FromBase64String(dataUrl[(comma + 1)..]), type); }
        catch (FormatException ex) { throw new InvalidDataException("The quote attachment could not be decoded.", ex); }
    }

    private static async Task<Issue?> DetailAsync(SqlDb db, Guid id)
    {
        var row = await db.Issues.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
        if (row is null) return null;
        var issue = (await MapAsync(db, [row]))[0];
        var files = await db.Quotes.AsNoTracking().Where(q => q.IssueId == id).Select(q => new { q.Id, q.ContentType, q.Data }).ToListAsync();
        foreach (var f in files)
        {
            var quote = issue.Quotes.FirstOrDefault(q => q.Id == f.Id.ToString());
            if (quote is not null) quote.FileDataUrl = $"data:{f.ContentType};base64,{Convert.ToBase64String(f.Data)}";
        }
        return issue;
    }

    // List views skip quote file bytes; DetailAsync adds them for a single issue.
    private static async Task<List<Issue>> MapAsync(SqlDb db, List<DbIssue> rows)
    {
        var ids = rows.Select(r => r.Id).ToList();
        var events = await db.IssueEvents.AsNoTracking().Where(e => ids.Contains(e.IssueId)).OrderBy(e => e.CreatedAt).ToListAsync();
        var media = await db.Media.AsNoTracking().Where(m => m.EventId != null && ids.Contains(m.IssueId!.Value))
            .Select(m => new { m.Id, m.EventId }).ToListAsync();
        var quotes = await db.Quotes.AsNoTracking().Where(q => ids.Contains(q.IssueId)).OrderBy(q => q.SubmittedAt)
            .Select(q => new { q.Id, q.IssueId, q.ContractorName, q.Amount, q.WorkDescription, q.FileName, q.Status, q.SubmittedBy, q.SubmittedAt }).ToListAsync();
        var submitterIds = quotes.Select(q => q.SubmittedBy).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => submitterIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName);
        static DateTime Utc(DateTime d) => DateTime.SpecifyKind(d, DateTimeKind.Utc);

        return rows.Select(r => new Issue
        {
            Id = r.Id.ToString(), Title = r.Title, Unit = r.UnitNumber, Severity = r.Severity, Status = r.Status,
            CreatedAt = Utc(r.CreatedAt), TenantId = r.TenantId.ToString(), LandlordId = r.LandlordId.ToString(),
            IsDisputed = r.IsDisputed, Description = r.Description, ContractorName = r.ContractorName,
            Events = events.Where(e => e.IssueId == r.Id).Select(e => new IssueEvent
            {
                Id = e.Id.ToString(), IssueId = e.IssueId.ToString(), EventType = e.EventType, Description = e.Description,
                Timestamp = Utc(e.CreatedAt), AuthorName = e.AuthorName, AuthorRole = e.AuthorRole, NewStatus = e.NewStatus,
                MediaUrls = media.Where(m => m.EventId == e.Id).Select(m => $"/sqlmedia/{m.Id}").ToList()
            }).ToList(),
            Quotes = quotes.Where(q => q.IssueId == r.Id).Select(q => new IssueQuote
            {
                Id = q.Id.ToString(), ContractorName = q.ContractorName, Amount = q.Amount, WorkDescription = q.WorkDescription,
                FileName = q.FileName, Status = q.Status, SubmittedBy = names.GetValueOrDefault(q.SubmittedBy, ""), SubmittedAt = Utc(q.SubmittedAt)
            }).ToList()
        }).ToList();
    }
}
