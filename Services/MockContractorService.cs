using RentLog.Models;

namespace RentLog.Services;

public sealed class MockContractorService(
    MockLandlordContractorManagementService managementService,
    MockIssueService issueService) : IContractorService
{
    private static readonly AuthUser DemoLandlord = new()
    {
        Id = "landlord-001",
        Name = "Jane Landlord",
        Role = UserRole.Landlord
    };

    public async Task<ContractorAccount> GetContractorProfileAsync(AuthUser contractor)
    {
        var profile = await FindProfileAsync(contractor);
        return profile;
    }

    public async Task UpdateContractorProfileAsync(
        AuthUser contractor,
        string serviceArea,
        IReadOnlyList<string> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);
        EnsureContractor(contractor);
        var profile = await FindProfileAsync(contractor);
        var normalizedTrades = trades
            .Where(trade => !string.IsNullOrWhiteSpace(trade))
            .Select(trade => trade.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedTrades.Count == 0 || normalizedTrades.Any(trade => trade.Length > 80) ||
            serviceArea.Trim().Length > 200)
        {
            throw new ArgumentException("Enter a service area and choose at least one valid trade.");
        }

        profile.ServiceArea = serviceArea.Trim();
        profile.Trades = normalizedTrades;
    }

    public async Task<IReadOnlyList<ContractorJob>> GetAvailableJobsAsync(AuthUser contractor)
    {
        var profile = await FindProfileAsync(contractor);
        if (!profile.Active)
        {
            return [];
        }
        var jobs = await issueService.GetLandlordJobsAsync(DemoLandlord);
        return jobs
            .Where(job => job.ReleasedToContractors &&
                job.ContractorStatus == ContractorStatus.OpenForContractors &&
                job.Category is not null &&
                profile.Trades.Contains(job.Category, StringComparer.OrdinalIgnoreCase))
            .OrderByDescending(job => job.Severity == Severity.Emergency)
            .ThenByDescending(job => job.Severity)
            .ThenByDescending(job => job.CreatedAt)
            .Select(SanitizePendingJob)
            .ToList();
    }

    public async Task<IReadOnlyList<ContractorJob>> GetMyJobsAsync(AuthUser contractor)
    {
        var profile = await FindProfileAsync(contractor);
        var jobs = await issueService.GetLandlordJobsAsync(DemoLandlord);
        return jobs.Where(job => job.AssignedContractorId == profile.Id)
            .Select(job => job.ContractorStatus == ContractorStatus.PendingApproval
                ? SanitizePendingJob(job)
                : job)
            .OrderByDescending(job => job.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<ContractorMedia>> GetJobMediaAsync(AuthUser contractor, string issueId)
    {
        var profile = await FindProfileAsync(contractor);
        var urls = issueService.GetContractorJobMedia(profile.Id, issueId);
        return urls.Select(url => new ContractorMedia
        {
            FileName = Path.GetFileName(new Uri(url, UriKind.RelativeOrAbsolute).IsAbsoluteUri
                ? new Uri(url).AbsolutePath
                : url),
            ContentType = GetMimeType(url),
            SignedUrl = url,
            CreatedAt = DateTime.UtcNow
        }).ToList();
    }

    public async Task AddJobUpdateAsync(
        AuthUser contractor,
        string issueId,
        string note,
        IReadOnlyList<string> mediaPaths)
    {
        var profile = await FindProfileAsync(contractor);
        if (note.Trim().Length > 2000 || (string.IsNullOrWhiteSpace(note) && mediaPaths.Count == 0))
        {
            throw new ArgumentException("Add a note or at least one photo or video.");
        }

        issueService.AddContractorUpdate(profile.Id, issueId, note, mediaPaths);
    }

    public async Task<ContractorJob> AcceptJobAsync(AuthUser contractor, string issueId)
    {
        var profile = await FindProfileAsync(contractor);
        if (!profile.Active)
        {
            throw new InvalidOperationException("The contractor is not active for this landlord.");
        }
        return issueService.AcceptContractorJob(contractor, profile, issueId);
    }

    public async Task WithdrawJobAsync(AuthUser contractor, string issueId)
    {
        var profile = await FindProfileAsync(contractor);
        issueService.WithdrawContractorJob(contractor, profile, issueId);
    }

    public async Task SetWorkStatusAsync(AuthUser contractor, string issueId, ContractorStatus status)
    {
        var profile = await FindProfileAsync(contractor);
        issueService.SetContractorWorkStatus(contractor, profile, issueId, status);
    }

    public Task VerifyWorkAsync(AuthUser tenant, string issueId) =>
        throw new NotSupportedException("Tenant verification is handled by the issue service in demo mode.");

    public Task ReopenWorkAsync(AuthUser tenant, string issueId, string note) =>
        throw new NotSupportedException("Tenant reopening is handled by the issue service in demo mode.");

    public Task SetJobReleasedAsync(AuthUser landlord, string issueId, bool released) =>
        issueService.SetJobReleasedAsync(landlord, issueId, released);

    public Task SetJobDetailsAsync(
        AuthUser landlord,
        string issueId,
        string category,
        IReadOnlyList<string> preferredWindows) =>
        issueService.SetJobDetailsAsync(landlord, issueId, category, preferredWindows);

    public Task ApproveJobAsync(AuthUser landlord, string issueId) =>
        issueService.ApproveJobAsync(landlord, issueId);

    public Task MarkJobSeenAsync(AuthUser landlord, string issueId) =>
        issueService.MarkJobSeenAsync(landlord, issueId);

    public Task ReturnJobToBoardAsync(AuthUser landlord, string issueId) =>
        issueService.ReturnJobToBoardAsync(landlord, issueId);

    public Task ReassignJobAsync(AuthUser landlord, string issueId, string contractorId) =>
        issueService.ReassignJobAsync(landlord, issueId, contractorId);

    public Task RedeemContractorInviteAsync(
        AuthUser contractor,
        string token,
        string displayName,
        IReadOnlyDictionary<string, string> contactDetails,
        string serviceArea,
        IReadOnlyList<string> trades)
    {
        managementService.RedeemContractorInvite(
            contractor, token, displayName, contactDetails, serviceArea, trades);
        return Task.CompletedTask;
    }

    public Task LinkContractorInviteAsync(AuthUser contractor, string token)
    {
        managementService.LinkContractorInvite(contractor, token);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LandlordNotification>> GetLandlordNotificationsAsync(AuthUser landlord) =>
        issueService.GetLandlordNotificationsAsync(landlord);

    public Task MarkNotificationReadAsync(AuthUser landlord, string notificationId) =>
        throw new NotSupportedException("Landlord notification actions are handled by the alerts workflow in demo mode.");

    public Task<IReadOnlyList<ContractorJob>> GetLandlordJobsAsync(AuthUser landlord) =>
        issueService.GetLandlordJobsAsync(landlord);

    public Task<IReadOnlyList<ContractorAccount>> GetLandlordContractorsAsync(AuthUser landlord) =>
        managementService.GetLandlordContractorsAsync(landlord);

    public Task SetLandlordContractorActiveAsync(AuthUser landlord, string contractorId, bool active) =>
        managementService.SetLandlordContractorActiveAsync(landlord, contractorId, active);

    public Task RemoveContractorAsync(AuthUser landlord, string contractorId) =>
        managementService.RemoveContractorAsync(landlord, contractorId);

    public Task<RegistrationInviteLink> CreateContractorInviteAsync(AuthUser landlord, TimeSpan? validity = null) =>
        managementService.CreateContractorInviteAsync(landlord, validity);

    public Task<RegistrationInviteLink> CreateUnitInviteAsync(AuthUser landlord, string unitId, TimeSpan? validity = null) =>
        managementService.CreateUnitInviteAsync(landlord, unitId, validity);

    public Task<IReadOnlyList<RegistrationInviteLink>> GetUnitInvitesAsync(AuthUser landlord, string unitId) =>
        managementService.GetUnitInvitesAsync(landlord, unitId);

    private async Task<ContractorAccount> FindProfileAsync(AuthUser contractor)
    {
        EnsureContractor(contractor);
        var accounts = await managementService.GetLandlordContractorsAsync(DemoLandlord);
        return accounts.FirstOrDefault(account => account.AuthUserId == contractor.Id)
            ?? throw new InvalidOperationException("The signed-in account has no contractor profile.");
    }

    private static ContractorJob SanitizePendingJob(ContractorJob job) => new()
    {
        Id = job.Id,
        IssueId = job.IssueId,
        Title = job.Title,
        Description = job.Description,
        LandlordId = job.LandlordId,
        LandlordLabel = job.LandlordLabel,
        PropertyName = job.PropertyName,
        Suburb = job.Suburb,
        ContractorName = job.ContractorName,
        Severity = job.Severity,
        ContractorStatus = job.ContractorStatus,
        Category = job.Category,
        CreatedAt = job.CreatedAt,
        AssignedContractorId = job.AssignedContractorId,
        AssignedToMe = job.AssignedToMe,
        ReopenNote = job.ReopenNote,
        ReleasedToContractors = job.ReleasedToContractors
    };

    private static void EnsureContractor(AuthUser contractor)
    {
        if (contractor.Role != UserRole.Contractor)
        {
            throw new InvalidOperationException("Only a contractor can perform this action.");
        }
    }

    private static string GetMimeType(string url) => Path.GetExtension(url).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".heic" => "image/heic",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        _ => "image/jpeg"
    };
}
