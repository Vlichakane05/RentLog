using RentLog.Models;
using System.Globalization;

namespace RentLog.Services;

public class MockIssueService : IIssueService, ILandlordContractorJobsService
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
            Category = "General Maintenance",
            PreferredWindows = ["2026-10-19 · Anytime"],
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
        },
        new Issue
        {
            Id = "issue-1007",
            Title = "Kitchen drain is blocked",
            Unit = "5A",
            Severity = Severity.Medium,
            Status = Status.Open,
            CreatedAt = new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc),
            TenantId = "tenant-005",
            LandlordId = "landlord-001",
            Description = "The kitchen sink is draining very slowly.",
            Category = "Plumbing",
            PreferredWindows = ["2026-10-20 · Morning", "2026-10-21 · Afternoon"],
            ReleasedToContractors = true,
            AssignedContractorId = "demo-contractor-1",
            ContractorName = "ClearFlow Plumbing",
            ContractorStatus = ContractorStatus.PendingApproval,
            AcceptedAt = new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc),
            Events =
            [
                Event("issue-1007", "StatusChange", "The kitchen sink is draining very slowly.", "Jordan Tenant", "Tenant", new DateTime(2026, 10, 17, 9, 0, 0, DateTimeKind.Utc), Status.Open)
            ]
        },
        new Issue
        {
            Id = "issue-1008",
            Title = "Water leaking through bathroom ceiling",
            Unit = "7C",
            Severity = Severity.Emergency,
            Status = Status.Open,
            CreatedAt = new DateTime(2026, 10, 18, 7, 30, 0, DateTimeKind.Utc),
            TenantId = "tenant-006",
            LandlordId = "landlord-001",
            Description = "Water is leaking from the ceiling near the bathroom light. Please inspect urgently.",
            Category = "Plumbing",
            PreferredWindows = ["2026-10-19 · Anytime"],
            ReleasedToContractors = true,
            ContractorStatus = ContractorStatus.OpenForContractors,
            Events =
            [
                Event("issue-1008", "StatusChange", "Water is leaking from the ceiling near the bathroom light. Please inspect urgently.", "Taylor Tenant", "Tenant", new DateTime(2026, 10, 18, 7, 30, 0, DateTimeKind.Utc), Status.Open)
            ]
        }
    ];
    private readonly List<LandlordNotification> _landlordNotifications = [];

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

    public Task<IReadOnlyList<ContractorJob>> GetLandlordJobsAsync(AuthUser landlord)
    {
        EnsureDemoLandlord(landlord);
        IReadOnlyList<ContractorJob> jobs = _issues
            .Where(issue => issue.LandlordId == landlord.Id)
            .Select(MapContractorJob)
            .ToList();
        return Task.FromResult(jobs);
    }

    public ContractorJob AcceptContractorJob(AuthUser contractor, ContractorAccount profile, string issueId)
    {
        EnsureContractorUser(contractor);
        var issue = _issues.FirstOrDefault(item => item.Id == issueId && item.LandlordId == "landlord-001");
        if (issue is null ||
            !issue.ReleasedToContractors ||
            issue.AssignedContractorId is not null ||
            issue.ContractorStatus != ContractorStatus.OpenForContractors ||
            issue.Category is null ||
            !profile.Trades.Contains(issue.Category, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Job already taken");
        }

        issue.AssignedContractorId = profile.Id;
        issue.ContractorName = profile.DisplayName;
        issue.ContractorStatus = ContractorStatus.PendingApproval;
        issue.AcceptedAt = DateTime.UtcNow;
        issue.Events.Add(Event(issue.Id, "Comment", "The contractor accepted this job and is waiting for landlord approval.",
            profile.DisplayName, "Contractor", DateTime.UtcNow));
        return MapContractorJob(issue);
    }

    public void WithdrawContractorJob(AuthUser contractor, ContractorAccount profile, string issueId)
    {
        EnsureContractorUser(contractor);
        var issue = FindAssignedContractorIssue(profile, issueId);
        if (issue.ContractorStatus is not (ContractorStatus.PendingApproval or ContractorStatus.Approved))
        {
            throw new InvalidOperationException("The job cannot be withdrawn after work has started.");
        }

        issue.AssignedContractorId = null;
        issue.ContractorName = null;
        issue.ContractorStatus = ContractorStatus.OpenForContractors;
        issue.AcceptedAt = null;
        issue.ReleasedToContractors = true;
        issue.Events.Add(Event(issue.Id, "Comment", "The contractor withdrew from this job.",
            profile.DisplayName, "Contractor", DateTime.UtcNow));
        _landlordNotifications.Add(new LandlordNotification
        {
            Id = Guid.NewGuid().ToString(),
            IssueId = issue.Id,
            Message = "The contractor withdrew from this job. It is open for contractors again.",
            CreatedAt = DateTime.UtcNow
        });
    }

    public void SetContractorWorkStatus(
        AuthUser contractor,
        ContractorAccount profile,
        string issueId,
        ContractorStatus status)
    {
        EnsureContractorUser(contractor);
        var issue = FindAssignedContractorIssue(profile, issueId);
        var validTransition = status == ContractorStatus.InProgress &&
                issue.ContractorStatus == ContractorStatus.Approved ||
            status == ContractorStatus.WorkDone &&
                issue.ContractorStatus == ContractorStatus.InProgress;
        if (!validTransition)
        {
            throw new InvalidOperationException("The contractor cannot apply this status update.");
        }

        issue.ContractorStatus = status;
        issue.WorkDoneAt = status == ContractorStatus.WorkDone ? DateTime.UtcNow : null;
        issue.TenantVerifiedAt = null;
        issue.Events.Add(Event(issue.Id, "StatusChange",
            status == ContractorStatus.WorkDone
                ? "The contractor marked the work done."
                : "The contractor started work.",
            profile.DisplayName, "Contractor", DateTime.UtcNow));
        if (status == ContractorStatus.WorkDone)
        {
            _landlordNotifications.Add(new LandlordNotification
            {
                Id = Guid.NewGuid().ToString(),
                IssueId = issue.Id,
                Message = "The contractor marked the work done. Please review the tenant issue.",
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    public void AddContractorUpdate(
        string contractorId,
        string issueId,
        string note,
        IReadOnlyList<string> mediaPaths)
    {
        var issue = _issues.FirstOrDefault(item => item.Id == issueId &&
            item.AssignedContractorId == contractorId &&
            item.ContractorStatus == ContractorStatus.InProgress)
            ?? throw new InvalidOperationException("Only the assigned contractor can update in-progress work.");
        var existingMedia = issue.Events.SelectMany(item => item.MediaUrls).ToList();
        var addedPhotos = mediaPaths.Count(path => GetMediaMimeType(path).StartsWith("image/", StringComparison.OrdinalIgnoreCase));
        var addedVideos = mediaPaths.Count(path => GetMediaMimeType(path).StartsWith("video/", StringComparison.OrdinalIgnoreCase));
        if (existingMedia.Count(path => GetMediaMimeType(path).StartsWith("image/", StringComparison.OrdinalIgnoreCase)) + addedPhotos > 5 ||
            existingMedia.Count(path => GetMediaMimeType(path).StartsWith("video/", StringComparison.OrdinalIgnoreCase)) + addedVideos > 2)
        {
            throw new InvalidOperationException("This issue has reached its photo or video limit.");
        }

        issue.Events.Add(Event(
            issue.Id,
            string.IsNullOrWhiteSpace(note) ? "MediaUpload" : "Comment",
            string.IsNullOrWhiteSpace(note) ? "Contractor uploaded job evidence." : note.Trim(),
            "Avery Plumbing",
            "Contractor",
            DateTime.UtcNow,
            mediaUrls: mediaPaths));
    }

    public IReadOnlyList<string> GetContractorJobMedia(string contractorId, string issueId) =>
        _issues.FirstOrDefault(item => item.Id == issueId && item.AssignedContractorId == contractorId)?
            .Events.SelectMany(item => item.MediaUrls).ToList() ?? [];

    public Task<IReadOnlyList<LandlordNotification>> GetLandlordNotificationsAsync(AuthUser landlord)
    {
        EnsureDemoLandlord(landlord);
        return Task.FromResult<IReadOnlyList<LandlordNotification>>(
            _landlordNotifications.Select(notification => new LandlordNotification
            {
                Id = notification.Id,
                IssueId = notification.IssueId,
                Message = notification.Message,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt
            }).ToList());
    }

    public Task MarkJobSeenAsync(AuthUser landlord, string issueId)
    {
        var issue = FindLandlordJob(landlord, issueId);
        issue.LandlordSeenAt ??= DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task SetJobReleasedAsync(AuthUser landlord, string issueId, bool released)
    {
        var issue = FindLandlordJob(landlord, issueId);
        var allowedStatus = released
            ? issue.ContractorStatus == ContractorStatus.Reported
            : issue.ContractorStatus == ContractorStatus.OpenForContractors;
        if (!allowedStatus || issue.AssignedContractorId is not null)
        {
            throw new InvalidOperationException("The issue is not eligible for this release change.");
        }

        issue.ReleasedToContractors = released;
        issue.ContractorStatus = released
            ? ContractorStatus.OpenForContractors
            : ContractorStatus.Reported;
        if (released)
        {
            issue.LandlordSeenAt ??= DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task SetJobDetailsAsync(
        AuthUser landlord,
        string issueId,
        string category,
        IReadOnlyList<string> preferredWindows)
    {
        var issue = FindLandlordJob(landlord, issueId);
        if (string.IsNullOrWhiteSpace(category) || preferredWindows is null ||
            issue.AssignedContractorId is not null ||
            issue.ContractorStatus is not (ContractorStatus.Reported or ContractorStatus.OpenForContractors))
        {
            throw new ArgumentException("A work category and available contractor job are required.");
        }

        issue.Category = category.Trim();
        issue.PreferredWindows = preferredWindows.ToList();
        return Task.CompletedTask;
    }

    public Task ApproveJobAsync(AuthUser landlord, string issueId)
    {
        var issue = FindLandlordJob(landlord, issueId);
        if (issue.ContractorStatus != ContractorStatus.PendingApproval || issue.AssignedContractorId is null)
        {
            throw new InvalidOperationException("The job is not awaiting approval.");
        }

        issue.ContractorStatus = ContractorStatus.Approved;
        issue.LandlordSeenAt ??= DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task ReturnJobToBoardAsync(AuthUser landlord, string issueId)
    {
        var issue = FindLandlordJob(landlord, issueId);
        if (issue.ContractorStatus != ContractorStatus.PendingApproval || issue.AssignedContractorId is null)
        {
            throw new InvalidOperationException("The job cannot be returned to the contractor board.");
        }

        issue.AssignedContractorId = null;
        issue.ContractorName = null;
        issue.ContractorStatus = ContractorStatus.OpenForContractors;
        issue.AcceptedAt = null;
        issue.ReleasedToContractors = true;
        issue.LandlordSeenAt ??= DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task ReassignJobAsync(AuthUser landlord, string issueId, string contractorId)
    {
        var issue = FindLandlordJob(landlord, issueId);
        if (issue.ContractorStatus != ContractorStatus.PendingApproval || string.IsNullOrWhiteSpace(contractorId))
        {
            throw new InvalidOperationException("The job cannot be reassigned to this contractor.");
        }

        issue.AssignedContractorId = contractorId;
        issue.ContractorName = contractorId == "demo-contractor-1" ? "Avery Plumbing" : "Bright Wire Electrical";
        issue.ContractorStatus = ContractorStatus.PendingApproval;
        issue.AcceptedAt = DateTime.UtcNow;
        issue.ReleasedToContractors = true;
        issue.LandlordSeenAt ??= DateTime.UtcNow;
        return Task.CompletedTask;
    }

    public Task<Issue> CreateIssueAsync(
        AuthUser tenant,
        string unit,
        string title,
        string description,
        Severity severity,
        string category,
        IReadOnlyList<string> preferredWindows,
        IReadOnlyList<string> mediaUrls)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenant.Role != UserRole.Tenant)
        {
            throw new InvalidOperationException("Only tenants can report an issue.");
        }
        if (string.IsNullOrWhiteSpace(category) ||
            preferredWindows is null || preferredWindows.Count == 0 ||
            preferredWindows.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("A work category and at least one preferred time window are required.");
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
            Description = description.Trim(),
            Category = category,
            PreferredWindows = preferredWindows.ToList()
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

    public Task<Issue?> VerifyContractorWorkAsync(AuthUser tenant, string issueId)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenant.Role != UserRole.Tenant)
        {
            throw new InvalidOperationException("Only a tenant can confirm contractor work.");
        }

        var issue = FindAuthorizedIssue(tenant, issueId);
        if (issue is null || issue.ContractorStatus != ContractorStatus.WorkDone)
        {
            return Task.FromResult<Issue?>(null);
        }

        issue.ContractorStatus = ContractorStatus.Completed;
        issue.TenantVerifiedAt = DateTime.UtcNow;
        issue.Events.Add(Event(
            issue.Id,
            "StatusChange",
            "The tenant confirmed the contractor work is fixed.",
            tenant.Name,
            tenant.Role.ToString(),
            DateTime.UtcNow));
        return Task.FromResult<Issue?>(Clone(issue));
    }

    public Task<Issue?> ReopenContractorWorkAsync(AuthUser tenant, string issueId, string note)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenant.Role != UserRole.Tenant)
        {
            throw new InvalidOperationException("Only a tenant can reopen contractor work.");
        }

        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > 500)
        {
            throw new ArgumentException("Enter a short note of up to 500 characters.", nameof(note));
        }

        var issue = FindAuthorizedIssue(tenant, issueId);
        if (issue is null || issue.ContractorStatus != ContractorStatus.WorkDone)
        {
            return Task.FromResult<Issue?>(null);
        }

        issue.ContractorStatus = ContractorStatus.InProgress;
        issue.WorkDoneAt = null;
        issue.TenantVerifiedAt = null;
        issue.ReopenNote = note.Trim();
        _landlordNotifications.Add(new LandlordNotification
        {
            Id = Guid.NewGuid().ToString(),
            IssueId = issue.Id,
            Message = "The tenant reopened the contractor work.",
            CreatedAt = DateTime.UtcNow
        });
        issue.Events.Add(Event(
            issue.Id,
            "Comment",
            $"The tenant reopened this work: {note.Trim()}",
            tenant.Name,
            tenant.Role.ToString(),
            DateTime.UtcNow));
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

    public Task<IssueQuote?> AddQuoteAsync(AuthUser landlord, string issueId, string contractorName, decimal amount, string workDescription, string fileName, string contentType, byte[] fileBytes)
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
            FileDataUrl = string.Empty,
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

    private Issue FindLandlordJob(AuthUser landlord, string issueId)
    {
        if (landlord.Role != UserRole.Landlord || landlord.Id != "landlord-001")
        {
            throw new InvalidOperationException("Only the demo landlord can manage contractor jobs in demo mode.");
        }

        return _issues.FirstOrDefault(issue =>
            issue.Id == issueId && issue.LandlordId == landlord.Id)
            ?? throw new InvalidOperationException("The job was not found for this landlord.");
    }

    private Issue FindAssignedContractorIssue(ContractorAccount profile, string issueId) =>
        _issues.FirstOrDefault(issue =>
            issue.Id == issueId && issue.AssignedContractorId == profile.Id)
        ?? throw new InvalidOperationException("The job is not assigned to this contractor.");

    private static void EnsureContractorUser(AuthUser contractor)
    {
        if (contractor.Role != UserRole.Contractor || contractor.Id != "contractor-user-001")
        {
            throw new InvalidOperationException("Only the demo contractor can manage these jobs.");
        }
    }

    private static ContractorJob MapContractorJob(Issue issue) => new()
    {
        Id = issue.Id,
        IssueId = issue.Id,
        Title = issue.Title,
        Description = issue.Description,
        LandlordId = issue.LandlordId,
        LandlordLabel = "Jane Landlord",
        PropertyName = "Harbour View Apartments",
        Suburb = "Sea Point",
        PropertyAddress = issue.ContractorStatus is ContractorStatus.Approved or ContractorStatus.InProgress or
            ContractorStatus.WorkDone or ContractorStatus.Completed
                ? "18 Ocean View Road, Sea Point"
                : null,
        UnitNumber = issue.ContractorStatus is ContractorStatus.Approved or ContractorStatus.InProgress or
            ContractorStatus.WorkDone or ContractorStatus.Completed
                ? issue.Unit
                : string.Empty,
        UnitId = issue.Unit,
        ContractorName = issue.ContractorName ?? string.Empty,
        AssignedContractorId = issue.AssignedContractorId,
        Category = issue.Category,
        PreferredWindows = [.. issue.PreferredWindows],
        ReopenNote = issue.ReopenNote,
        ReleasedToContractors = issue.ReleasedToContractors,
        ContractorStatus = issue.ContractorStatus,
        Severity = issue.Severity,
        CreatedAt = issue.CreatedAt,
        AcceptedAt = issue.AcceptedAt,
        LandlordSeenAt = issue.LandlordSeenAt,
        WorkDoneAt = issue.WorkDoneAt,
        TenantVerifiedAt = issue.TenantVerifiedAt,
        HasAssignedContractor = issue.AssignedContractorId is not null
    };

    private static string GetMediaMimeType(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".heic" => "image/heic",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        _ => "image/jpeg"
    };

    private static void EnsureDemoLandlord(AuthUser landlord)
    {
        if (landlord.Role != UserRole.Landlord || landlord.Id != "landlord-001")
        {
            throw new InvalidOperationException("Only the demo landlord can manage contractor jobs in demo mode.");
        }
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
            ContractorFirstName = issue.ContractorFirstName,
            ContractorTrade = issue.ContractorTrade,
            Category = issue.Category,
            PreferredWindows = [.. issue.PreferredWindows],
            ReleasedToContractors = issue.ReleasedToContractors,
            AssignedContractorId = issue.AssignedContractorId,
            ContractorStatus = issue.ContractorStatus,
            AcceptedAt = issue.AcceptedAt,
            LandlordSeenAt = issue.LandlordSeenAt,
            WorkDoneAt = issue.WorkDoneAt,
            TenantVerifiedAt = issue.TenantVerifiedAt,
            ReopenNote = issue.ReopenNote,
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
