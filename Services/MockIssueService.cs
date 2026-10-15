using RentLog.Models;
using System.Globalization;

namespace RentLog.Services;

public class MockIssueService : IIssueService
{
    private static readonly CultureInfo _moneyCulture = CultureInfo.GetCultureInfo("en-US");
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
            ContractorName = "ClearFlow Plumbing",
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
            ContractorName = "Coastal Window Repairs",
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
        },
        new Issue
        {
            Id = "issue-1004",
            Title = "Water coming through ceiling",
            Unit = "2A",
            Severity = Severity.Emergency,
            Status = Status.Open,
            CreatedAt = new DateTime(2026, 10, 15, 6, 10, 0, DateTimeKind.Utc),
            TenantId = "tenant-002",
            LandlordId = "landlord-001",
            Description = "Water is leaking through the bedroom ceiling near the light fitting.",
            Events =
            [
                Event("issue-1004", "StatusChange", "Water is leaking through the bedroom ceiling near the light fitting.", "Alex Tenant", "Tenant", new DateTime(2026, 10, 15, 6, 10, 0, DateTimeKind.Utc), Status.Open)
            ]
        },
        new Issue
        {
            Id = "issue-1005",
            Title = "Front gate lock is jammed",
            Unit = "1C",
            Severity = Severity.High,
            Status = Status.Disputed,
            IsDisputed = true,
            CreatedAt = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc),
            TenantId = "tenant-003",
            LandlordId = "landlord-001",
            Description = "The front gate lock still sticks after the repair.",
            Events =
            [
                Event("issue-1005", "StatusChange", "The front gate lock still sticks after the repair.", "Sam Tenant", "Tenant", new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc), Status.Open),
                Event("issue-1005", "StatusChange", "The lock was repaired. Please check it.", "Jane Landlord", "Landlord", new DateTime(2026, 10, 13, 13, 0, 0, DateTimeKind.Utc), Status.AwaitingConfirmation),
                Event("issue-1005", "StatusChange", "The tenant disputed the resolution.", "Sam Tenant", "Tenant", new DateTime(2026, 10, 14, 8, 30, 0, DateTimeKind.Utc), Status.Disputed),
                Event("issue-1005", "Comment", "The gate still gets stuck and is hard to lock.", "Sam Tenant", "Tenant", new DateTime(2026, 10, 14, 8, 31, 0, DateTimeKind.Utc))
            ]
        },
        new Issue
        {
            Id = "issue-1006",
            Title = "Replace damaged kitchen cabinet",
            Unit = "4D",
            Severity = Severity.Medium,
            Status = Status.Acknowledged,
            CreatedAt = new DateTime(2026, 10, 9, 11, 0, 0, DateTimeKind.Utc),
            TenantId = "tenant-004",
            LandlordId = "landlord-001",
            Description = "The cabinet door has come off its hinges.",
            ContractorName = "Cape Fix Contractors",
            Quotes =
            [
                new IssueQuote
                {
                    Id = "quote-6001",
                    ContractorName = "Cape Fix Contractors",
                    Amount = 1250m,
                    WorkDescription = "Replace hinges and rehang cabinet door.",
                    FileName = "kitchen-cabinet-quote.pdf",
                    Status = "Pending approval",
                    SubmittedBy = "Jane Landlord",
                    SubmittedAt = new DateTime(2026, 10, 10, 9, 30, 0, DateTimeKind.Utc)
                }
            ],
            Events =
            [
                Event("issue-1006", "StatusChange", "The cabinet door has come off its hinges.", "Morgan Tenant", "Tenant", new DateTime(2026, 10, 9, 11, 0, 0, DateTimeKind.Utc), Status.Open),
                Event("issue-1006", "StatusChange", "We are reviewing a contractor quote.", "Jane Landlord", "Landlord", new DateTime(2026, 10, 10, 9, 30, 0, DateTimeKind.Utc), Status.Acknowledged)
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

    public Task<IReadOnlyList<Issue>> GetLandlordIssuesAsync(string landlordId)
    {
        IReadOnlyList<Issue> issues = _issues
            .Where(issue => issue.LandlordId == landlordId)
            .OrderByDescending(issue => issue.Severity)
            .ThenByDescending(issue => issue.CreatedAt)
            .Select(Clone)
            .ToList();
        return Task.FromResult(issues);
    }

    public Task<Issue?> GetLandlordIssueAsync(string landlordId, string issueId)
    {
        var issue = _issues.FirstOrDefault(item => item.Id == issueId && item.LandlordId == landlordId);
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
            LandlordId = "landlord-001",
            Description = description.Trim()
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
        if (issue is null || issue.Status != Status.AwaitingConfirmation)
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

    public Task<Issue?> AcknowledgeAsync(AuthUser landlord, string issueId)
    {
        var issue = GetAuthorizedLandlordIssue(landlord, issueId);
        if (issue is null || issue.Status != Status.Open)
        {
            return Task.FromResult<Issue?>(null);
        }

        SetStatus(issue, landlord, Status.Acknowledged, "The landlord acknowledged this issue.");
        return Task.FromResult<Issue?>(Clone(issue));
    }

    public Task<IssueQuote?> AddQuoteAsync(AuthUser landlord, string issueId, string contractorName, decimal amount, string workDescription, string fileName, string fileDataUrl)
    {
        var issue = GetAuthorizedLandlordIssue(landlord, issueId);
        if (issue is null || issue.Status is Status.Resolved or Status.Disputed)
        {
            return Task.FromResult<IssueQuote?>(null);
        }

        var quote = new IssueQuote
        {
            Id = $"quote-{Guid.NewGuid():N}",
            ContractorName = contractorName.Trim(),
            Amount = amount,
            WorkDescription = workDescription.Trim(),
            FileName = Path.GetFileName(fileName),
            FileDataUrl = fileDataUrl,
            Status = "Pending approval",
            SubmittedBy = landlord.Name,
            SubmittedAt = DateTime.UtcNow
        };
        issue.Quotes.Add(quote);
        issue.ContractorName = quote.ContractorName;
        issue.Events.Add(Event(issue.Id, "Quote", $"Quote from {quote.ContractorName} for R {quote.Amount.ToString("N2", _moneyCulture)}: {quote.WorkDescription} ({quote.FileName})", landlord.Name, landlord.Role.ToString(), quote.SubmittedAt));
        return Task.FromResult<IssueQuote?>(CloneQuote(quote));
    }

    public Task<Issue?> ApproveQuoteAsync(AuthUser landlord, string issueId, string quoteId)
    {
        var issue = GetAuthorizedLandlordIssue(landlord, issueId);
        var quote = issue?.Quotes.FirstOrDefault(item => item.Id == quoteId);
        if (issue is null || quote is null || quote.Status != "Pending approval")
        {
            return Task.FromResult<Issue?>(null);
        }

        quote.Status = "Approved";
        issue.ContractorName = quote.ContractorName;
        issue.Events.Add(Event(issue.Id, "Quote", $"The quote from {quote.ContractorName} for R {quote.Amount.ToString("N2", _moneyCulture)} was approved.", landlord.Name, landlord.Role.ToString(), DateTime.UtcNow));
        if (issue.Status == Status.Acknowledged)
        {
            SetStatus(issue, landlord, Status.InProgress, $"{quote.ContractorName} was approved to complete the quoted work.");
        }

        return Task.FromResult<Issue?>(Clone(issue));
    }

    public Task<Issue?> MarkFixedAsync(AuthUser landlord, string issueId, string comment)
    {
        var issue = GetAuthorizedLandlordIssue(landlord, issueId);
        if (issue is null || issue.Status is Status.Open or Status.Resolved or Status.Disputed)
        {
            return Task.FromResult<Issue?>(null);
        }

        issue.Events.Add(Event(issue.Id, "Comment", comment.Trim(), landlord.Name, landlord.Role.ToString(), DateTime.UtcNow));
        SetStatus(issue, landlord, Status.AwaitingConfirmation, "The landlord marked this issue fixed. Please confirm the repair.");
        return Task.FromResult<Issue?>(Clone(issue));
    }

    public Task<Issue?> RespondToDisputeAsync(AuthUser landlord, string issueId, string response, bool agreeResolved)
    {
        var issue = GetAuthorizedLandlordIssue(landlord, issueId);
        if (issue is null || issue.Status != Status.Disputed)
        {
            return Task.FromResult<Issue?>(null);
        }

        issue.Events.Add(Event(issue.Id, "Comment", response.Trim(), landlord.Name, landlord.Role.ToString(), DateTime.UtcNow));
        if (agreeResolved)
        {
            SetStatus(issue, landlord, Status.AwaitingConfirmation, "The landlord responded to the dispute and marked the issue fixed again for tenant confirmation.");
        }
        else
        {
            SetStatus(issue, landlord, Status.InProgress, "The landlord will continue work on the disputed issue.");
        }

        issue.IsDisputed = false;
        return Task.FromResult<Issue?>(Clone(issue));
    }

    private Issue? GetAuthorizedLandlordIssue(AuthUser landlord, string issueId)
    {
        ArgumentNullException.ThrowIfNull(landlord);
        if (landlord.Role != UserRole.Landlord)
        {
            throw new InvalidOperationException("Only a landlord can update landlord issues.");
        }

        return _issues.FirstOrDefault(item => item.Id == issueId && item.LandlordId == landlord.Id);
    }

    private static void SetStatus(Issue issue, AuthUser author, Status status, string description)
    {
        issue.Status = status;
        issue.Events.Add(Event(issue.Id, "StatusChange", description, author.Name, author.Role.ToString(), DateTime.UtcNow, status));
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
            Description = issue.Description,
            ContractorName = issue.ContractorName,
            Quotes = issue.Quotes.Select(CloneQuote).ToList(),
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

    private static IssueQuote CloneQuote(IssueQuote quote)
    {
        return new IssueQuote
        {
            Id = quote.Id,
            ContractorName = quote.ContractorName,
            Amount = quote.Amount,
            WorkDescription = quote.WorkDescription,
            FileName = quote.FileName,
            FileDataUrl = quote.FileDataUrl,
            Status = quote.Status,
            SubmittedBy = quote.SubmittedBy,
            SubmittedAt = quote.SubmittedAt
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
