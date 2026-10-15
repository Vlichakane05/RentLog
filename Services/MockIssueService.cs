using RentLog.Models;

namespace RentLog.Services;

public class MockIssueService : IIssueService
{
    private readonly List<Issue> _issues =
    [
        new Issue
        {
            Id = "issue-1001",
            Title = "Leaking kitchen tap",
            Unit = "3B",
            Severity = Severity.Medium,
            Status = Status.InProgress,
            CreatedAt = new DateTime(2026, 10, 8, 8, 30, 0, DateTimeKind.Utc),
            TenantId = "tenant-001",
            LandlordId = "landlord-001",
            Events =
            [
                Event("issue-1001", "StatusChange", "Issue reported", "John Tenant", "Tenant", new DateTime(2026, 10, 8, 8, 30, 0, DateTimeKind.Utc), Status.Open),
                Event("issue-1001", "Comment", "Thanks for reporting this. I have contacted a plumber.", "Jane Landlord", "Landlord", new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc)),
                Event("issue-1001", "StatusChange", "A plumber has been scheduled.", "Jane Landlord", "Landlord", new DateTime(2026, 10, 9, 9, 15, 0, DateTimeKind.Utc), Status.InProgress)
            ]
        },
        new Issue
        {
            Id = "issue-1002",
            Title = "Bedroom window will not close",
            Unit = "3B",
            Severity = Severity.High,
            Status = Status.AwaitingConfirmation,
            CreatedAt = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc),
            TenantId = "tenant-001",
            LandlordId = "landlord-001",
            Events =
            [
                Event("issue-1002", "StatusChange", "Issue reported", "John Tenant", "Tenant", new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc), Status.Open),
                Event("issue-1002", "StatusChange", "Maintenance has replaced the window latch. Please check that it closes properly.", "Jane Landlord", "Landlord", new DateTime(2026, 10, 10, 14, 0, 0, DateTimeKind.Utc), Status.AwaitingConfirmation)
            ]
        },
        new Issue
        {
            Id = "issue-1003",
            Title = "Bathroom light not working",
            Unit = "3B",
            Severity = Severity.Low,
            Status = Status.Resolved,
            CreatedAt = new DateTime(2026, 8, 3, 7, 45, 0, DateTimeKind.Utc),
            TenantId = "tenant-001",
            LandlordId = "landlord-001",
            Events =
            [
                Event("issue-1003", "StatusChange", "Issue reported", "John Tenant", "Tenant", new DateTime(2026, 8, 3, 7, 45, 0, DateTimeKind.Utc), Status.Open),
                Event("issue-1003", "StatusChange", "The bulb has been replaced and the light is working.", "Jane Landlord", "Landlord", new DateTime(2026, 8, 4, 11, 20, 0, DateTimeKind.Utc), Status.Resolved)
            ]
        }
    ];

    public Task<IReadOnlyList<Issue>> GetTenantIssuesAsync(string tenantId)
    {
        IReadOnlyList<Issue> issues = _issues
            .Where(issue => issue.TenantId == tenantId)
            .OrderByDescending(issue => issue.CreatedAt)
            .Select(Clone)
            .ToList();
        return Task.FromResult(issues);
    }

    public Task<Issue?> GetTenantIssueAsync(string tenantId, string issueId)
    {
        var issue = _issues.FirstOrDefault(item => item.Id == issueId && item.TenantId == tenantId);
        return Task.FromResult(issue is null ? null : Clone(issue));
    }

    public Task<Issue> CreateIssueAsync(AuthUser tenant, string unit, string title, string description, Severity severity, IReadOnlyList<string> mediaUrls)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenant.Role != UserRole.Tenant)
        {
            throw new InvalidOperationException("Only tenants can report an issue.");
        }

        var issue = new Issue
        {
            Id = $"issue-{Guid.NewGuid():N}",
            Title = title.Trim(),
            Unit = unit.Trim(),
            Severity = severity,
            Status = Status.Open,
            CreatedAt = DateTime.UtcNow,
            TenantId = tenant.Id,
            LandlordId = "landlord-001"
        };

        issue.Events.Add(Event(issue.Id, "StatusChange", description.Trim(), tenant.Name, tenant.Role.ToString(), issue.CreatedAt, Status.Open));
        for (var index = 0; index < mediaUrls.Count; index++)
        {
            issue.Events.Add(Event(
                issue.Id,
                "MediaUpload",
                "Evidence attached to this issue.",
                tenant.Name,
                tenant.Role.ToString(),
                issue.CreatedAt.AddMilliseconds(index + 1),
                mediaUrls: [mediaUrls[index]]));
        }

        _issues.Add(issue);
        return Task.FromResult(Clone(issue));
    }

    public Task<Issue?> AddCommentAsync(AuthUser author, string issueId, string comment)
    {
        ArgumentNullException.ThrowIfNull(author);
        var issue = FindAuthorizedIssue(author, issueId);
        if (issue is null)
        {
            return Task.FromResult<Issue?>(null);
        }

        issue.Events.Add(Event(issue.Id, "Comment", comment.Trim(), author.Name, author.Role.ToString(), DateTime.UtcNow));
        return Task.FromResult<Issue?>(Clone(issue));
    }

    public Task<Issue?> ConfirmResolutionAsync(AuthUser tenant, string issueId)
    {
        if (tenant.Role != UserRole.Tenant)
        {
            throw new InvalidOperationException("Only the tenant can confirm resolution.");
        }

        var issue = _issues.FirstOrDefault(item => item.Id == issueId && item.TenantId == tenant.Id);
        if (issue is null || issue.Status is not (Status.AwaitingConfirmation or Status.Disputed))
        {
            return Task.FromResult<Issue?>(null);
        }

        issue.Status = Status.Resolved;
        issue.IsDisputed = false;
        issue.Events.Add(Event(issue.Id, "StatusChange", "The tenant confirmed the issue is resolved.", tenant.Name, tenant.Role.ToString(), DateTime.UtcNow, Status.Resolved));
        return Task.FromResult<Issue?>(Clone(issue));
    }

    public Task<Issue?> DisputeResolutionAsync(AuthUser tenant, string issueId, string reason)
    {
        if (tenant.Role != UserRole.Tenant)
        {
            throw new InvalidOperationException("Only the tenant can dispute a resolution.");
        }

        var issue = _issues.FirstOrDefault(item => item.Id == issueId && item.TenantId == tenant.Id);
        if (issue is null || issue.Status != Status.AwaitingConfirmation)
        {
            return Task.FromResult<Issue?>(null);
        }

        issue.Status = Status.Disputed;
        issue.IsDisputed = true;
        issue.Events.Add(Event(issue.Id, "StatusChange", "The tenant disputed the resolution.", tenant.Name, tenant.Role.ToString(), DateTime.UtcNow, Status.Disputed));
        issue.Events.Add(Event(issue.Id, "Comment", reason.Trim(), tenant.Name, tenant.Role.ToString(), DateTime.UtcNow));
        return Task.FromResult<Issue?>(Clone(issue));
    }

    private Issue? FindAuthorizedIssue(AuthUser author, string issueId)
    {
        return _issues.FirstOrDefault(issue =>
            issue.Id == issueId &&
            (author.Role == UserRole.Tenant
                ? issue.TenantId == author.Id
                : author.Role == UserRole.Landlord && issue.LandlordId == author.Id));
    }

    private static Issue Clone(Issue issue)
    {
        return new Issue
        {
            Id = issue.Id,
            Title = issue.Title,
            Unit = issue.Unit,
            Severity = issue.Severity,
            Status = issue.Status,
            CreatedAt = issue.CreatedAt,
            TenantId = issue.TenantId,
            LandlordId = issue.LandlordId,
            IsDisputed = issue.IsDisputed,
            Events = issue.Events.Select(item => new IssueEvent
            {
                Id = item.Id,
                IssueId = item.IssueId,
                EventType = item.EventType,
                Description = item.Description,
                Timestamp = item.Timestamp,
                AuthorName = item.AuthorName,
                AuthorRole = item.AuthorRole,
                NewStatus = item.NewStatus,
                MediaUrls = [.. item.MediaUrls]
            }).ToList()
        };
    }

    private static IssueEvent Event(
        string issueId,
        string eventType,
        string description,
        string authorName,
        string authorRole,
        DateTime timestamp,
        Status? status = null,
        IReadOnlyList<string>? mediaUrls = null)
    {
        return new IssueEvent
        {
            Id = Guid.NewGuid().ToString("N"),
            IssueId = issueId,
            EventType = eventType,
            Description = description,
            AuthorName = authorName,
            AuthorRole = authorRole,
            Timestamp = timestamp,
            NewStatus = status,
            MediaUrls = mediaUrls?.ToList() ?? []
        };
    }
}
