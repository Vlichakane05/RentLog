using System.Security.Cryptography;
using RentLog.Models;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public sealed class SupabaseContractorService(
    SupabaseClientProvider clientProvider,
    SupabaseMediaService mediaService) :
    IContractorService,
    ILandlordContractorManagementService,
    ILandlordContractorJobsService
{
    public async Task<IReadOnlyList<ContractorJob>> GetAvailableJobsAsync(AuthUser contractor)
    {
        var (client, _) = await GetContractorAsync(contractor);
        var jobs = (await client.From<ContractorJobRow>()
            .Order("created_at", Ordering.Descending)
            .Get()).Models;
        return jobs
            .Where(job => !job.AssignedToMe)
            .OrderByDescending(job => string.Equals(job.Severity, nameof(Severity.Emergency), StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(job => ParseSeverity(job.Severity))
            .ThenByDescending(job => job.CreatedAt)
            .Select(MapJob)
            .ToList();
    }

    public async Task<IReadOnlyList<ContractorJob>> GetMyJobsAsync(AuthUser contractor)
    {
        var (client, _) = await GetContractorAsync(contractor);
        var jobs = (await client.From<ContractorJobRow>()
            .Order("created_at", Ordering.Descending)
            .Get()).Models;
        return jobs
            .Where(job => job.AssignedToMe)
            .OrderByDescending(job => job.CreatedAt)
            .Select(MapJob)
            .ToList();
    }

    public async Task<ContractorAccount> GetContractorProfileAsync(AuthUser contractor)
    {
        var (client, profile) = await GetContractorAsync(contractor);
        var trades = (await client.From<ContractorTradeRow>()
            .Filter("contractor_id", Operator.Equals, profile.Id.ToString())
            .Order("category", Ordering.Ascending)
            .Get()).Models.Select(trade => trade.Category).ToList();
        return new ContractorAccount
        {
            Id = profile.Id.ToString(),
            AuthUserId = profile.AuthUserId.ToString(),
            DisplayName = profile.DisplayName,
            ContactDetails = profile.ContactDetails,
            ServiceArea = profile.ServiceArea,
            Trades = trades
        };
    }

    public async Task UpdateContractorProfileAsync(
        AuthUser contractor,
        string serviceArea,
        IReadOnlyList<string> trades)
    {
        ArgumentNullException.ThrowIfNull(trades);
        var normalizedTrades = trades
            .Where(trade => !string.IsNullOrWhiteSpace(trade))
            .Select(trade => trade.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedTrades.Length == 0 || normalizedTrades.Any(trade => trade.Length > 80) ||
            serviceArea is null || serviceArea.Trim().Length > 200)
        {
            throw new ArgumentException("Enter a service area and choose at least one valid trade.");
        }

        var (client, _) = await GetContractorAsync(contractor);
        await client.Rpc("update_contractor_profile", new
        {
            p_service_area = serviceArea.Trim(),
            p_trades = normalizedTrades
        });
    }

    public async Task<IReadOnlyList<ContractorMedia>> GetJobMediaAsync(AuthUser contractor, string issueId)
    {
        var (client, _) = await GetContractorAsync(contractor);
        var media = (await client.From<ContractorMediaRow>()
            .Filter("issue_id", Operator.Equals, ParseId(issueId, "issue").ToString())
            .Order("created_at", Ordering.Ascending)
            .Get()).Models;
        var result = new List<ContractorMedia>(media.Count);
        foreach (var item in media)
        {
            var signedUrl = await client.Storage
                .From("issue-media")
                .CreateSignedUrl(item.StoragePath, 3600);
            result.Add(new ContractorMedia
            {
                FileName = item.FileName,
                ContentType = item.MimeType,
                SignedUrl = signedUrl,
                CreatedAt = AsUtc(item.CreatedAt)
            });
        }

        return result;
    }

    public async Task AddJobUpdateAsync(
        AuthUser contractor,
        string issueId,
        string note,
        IReadOnlyList<string> mediaPaths)
    {
        ArgumentNullException.ThrowIfNull(mediaPaths);
        if (note?.Trim().Length > 2000)
        {
            throw new ArgumentException("Job notes must not exceed 2,000 characters.", nameof(note));
        }

        var (client, _) = await GetContractorAsync(contractor);
        var authUserId = ParseId(contractor.Id, "contractor user");
        var items = new List<object>(mediaPaths.Count);
        foreach (var path in mediaPaths.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !path.StartsWith($"pending/{authUserId:D}/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The uploaded evidence does not belong to this contractor.");
            }
            if (!mediaService.TryGetUploadedMetadata(path, out var metadata))
            {
                throw new InvalidOperationException("The evidence upload details expired. Upload the files again.");
            }

            items.Add(new
            {
                storage_path = path,
                file_name = metadata.FileName,
                mime_type = metadata.MimeType,
                file_size_bytes = metadata.FileSizeBytes,
                alt_text = metadata.AltText,
                duration_seconds = metadata.DurationSeconds
            });
        }

        if (string.IsNullOrWhiteSpace(note) && items.Count == 0)
        {
            throw new ArgumentException("Add a note or at least one photo or video.", nameof(note));
        }

        await client.Rpc("add_contractor_job_update", new
        {
            p_issue_id = ParseId(issueId, "issue"),
            p_note = note?.Trim() ?? string.Empty,
            p_media = items
        });
        mediaService.ClearUploadedMetadata(mediaPaths);
    }

    public async Task<IReadOnlyList<ContractorJob>> GetLandlordJobsAsync(AuthUser landlord)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        var rows = (await client.From<IssueRow>()
            .Filter("landlord_id", Operator.Equals, ParseId(landlord.Id, "landlord").ToString())
            .Order("created_at", Ordering.Descending)
            .Get()).Models;
        var jobs = new List<ContractorJob>();
        foreach (var row in rows.Where(issue =>
            issue.Category is not null || issue.ReleasedToContractors || issue.AssignedContractorId is not null))
        {
            var contractorName = row.AssignedContractorId is null
                ? null
                : (await client.From<ContractorRow>()
                    .Filter("id", Operator.Equals, row.AssignedContractorId.Value.ToString())
                    .Get()).Models.FirstOrDefault()?.DisplayName;
            var property = (await client.From<PropertyRow>()
                .Filter("id", Operator.Equals, row.PropertyId.ToString())
                .Get()).Models.FirstOrDefault();
            jobs.Add(new ContractorJob
            {
                Id = row.Id.ToString(),
                IssueId = row.Id.ToString(),
                Title = row.Title,
                Description = row.Description,
                CreatedAt = AsUtc(row.CreatedAt),
                LandlordId = row.LandlordId.ToString(),
                LandlordLabel = landlord.Name,
                PropertyName = property?.Name ?? string.Empty,
                PropertyAddress = property?.Address,
                Suburb = ExtractSuburb(property?.Address),
                ReopenNote = row.ReopenNote,
                UnitNumber = row.UnitNumber,
                UnitId = row.UnitId.ToString(),
                ContractorName = contractorName ?? string.Empty,
                AssignedContractorId = row.AssignedContractorId?.ToString(),
                Category = row.Category,
                PreferredWindows = row.PreferredWindows,
                ContractorStatus = ParseContractorStatus(row.ContractorStatus),
                Severity = Enum.TryParse<Severity>(row.Severity, true, out var severity)
                    ? severity
                    : throw new InvalidDataException($"Unsupported issue severity '{row.Severity}' in Supabase."),
                AcceptedAt = row.AcceptedAt is null ? null : AsUtc(row.AcceptedAt.Value),
                LandlordSeenAt = row.LandlordSeenAt is null ? null : AsUtc(row.LandlordSeenAt.Value),
                WorkDoneAt = row.WorkDoneAt is null ? null : AsUtc(row.WorkDoneAt.Value),
                TenantVerifiedAt = row.TenantVerifiedAt is null ? null : AsUtc(row.TenantVerifiedAt.Value),
                HasAssignedContractor = row.AssignedContractorId is not null
            });
        }

        return jobs;
    }

    public async Task<ContractorJob> AcceptJobAsync(AuthUser contractor, string issueId)
    {
        var (client, _) = await GetContractorAsync(contractor);
        var response = await client.Rpc<ContractorJobRow>(
            "accept_contractor_job",
            new { p_issue_id = ParseId(issueId, "issue") });
        return response is null
            ? throw new InvalidOperationException("Job already taken")
            : MapJob(response);
    }

    public async Task WithdrawJobAsync(AuthUser contractor, string issueId)
    {
        var (client, _) = await GetContractorAsync(contractor);
        await client.Rpc("withdraw_contractor_job", new { p_issue_id = ParseId(issueId, "issue") });
    }

    public async Task SetWorkStatusAsync(AuthUser contractor, string issueId, ContractorStatus status)
    {
        if (status is not (ContractorStatus.InProgress or ContractorStatus.WorkDone))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Contractors can set work to In progress or Work done.");
        }

        var (client, _) = await GetContractorAsync(contractor);
        await client.Rpc("set_contractor_work_status", new
        {
            p_issue_id = ParseId(issueId, "issue"),
            p_status = ToDatabaseContractorStatus(status)
        });
    }

    public async Task VerifyWorkAsync(AuthUser tenant, string issueId)
    {
        var client = await GetRoleClientAsync(tenant, UserRole.Tenant);
        await client.Rpc("verify_contractor_work", new { p_issue_id = ParseId(issueId, "issue") });
    }

    public async Task ReopenWorkAsync(AuthUser tenant, string issueId, string note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            throw new ArgumentException("A reopening note is required.", nameof(note));
        }

        var client = await GetRoleClientAsync(tenant, UserRole.Tenant);
        await client.Rpc("reopen_contractor_work", new
        {
            p_issue_id = ParseId(issueId, "issue"),
            p_note = note.Trim()
        });
    }

    public async Task SetJobReleasedAsync(AuthUser landlord, string issueId, bool released)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("release_contractor_job", new
        {
            p_issue_id = ParseId(issueId, "issue"),
            p_release = released
        });
    }

    public async Task SetJobDetailsAsync(
        AuthUser landlord,
        string issueId,
        string category,
        IReadOnlyList<string> preferredWindows)
    {
        if (string.IsNullOrWhiteSpace(category) || preferredWindows is null)
        {
            throw new ArgumentException("A job category and preferred work windows are required.");
        }

        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("set_contractor_issue_details", new
        {
            p_issue_id = ParseId(issueId, "issue"),
            p_category = category.Trim(),
            p_preferred_windows = preferredWindows
        });
    }

    public async Task ApproveJobAsync(AuthUser landlord, string issueId)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("approve_contractor_job", new { p_issue_id = ParseId(issueId, "issue") });
    }

    public async Task MarkJobSeenAsync(AuthUser landlord, string issueId)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("mark_contractor_job_seen", new { p_issue_id = ParseId(issueId, "issue") });
    }

    public async Task ReturnJobToBoardAsync(AuthUser landlord, string issueId)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("return_contractor_job_to_board", new
        {
            p_issue_id = ParseId(issueId, "issue")
        });
    }

    public async Task ReassignJobAsync(AuthUser landlord, string issueId, string contractorId)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("reassign_contractor_job", new
        {
            p_issue_id = ParseId(issueId, "issue"),
            p_contractor_id = ParseId(contractorId, "contractor")
        });
    }

    public async Task<IReadOnlyList<ContractorAccount>> GetLandlordContractorsAsync(AuthUser landlord)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        var landlordId = ParseId(landlord.Id, "landlord");
        var links = (await client.From<LandlordContractorRow>()
            .Filter("landlord_id", Operator.Equals, landlordId.ToString())
            .Order("date_added", Ordering.Descending)
            .Get()).Models;
        var contractors = new List<ContractorAccount>(links.Count);
        foreach (var link in links)
        {
            var contractor = (await client.From<ContractorRow>()
                .Filter("id", Operator.Equals, link.ContractorId.ToString())
                .Get()).Models.FirstOrDefault();
            if (contractor is null)
            {
                throw new InvalidDataException($"Contractor profile {link.ContractorId} is missing.");
            }

            var trades = (await client.From<ContractorTradeRow>()
                .Filter("contractor_id", Operator.Equals, link.ContractorId.ToString())
                .Get()).Models.Select(trade => trade.Category).ToList();
            var accessibleUnitIds = (await client.From<ContractorUnitAccessRow>()
                .Filter("landlord_id", Operator.Equals, landlordId.ToString())
                .Filter("contractor_id", Operator.Equals, link.ContractorId.ToString())
                .Get()).Models.Select(access => access.UnitId.ToString()).ToList();
            contractors.Add(new ContractorAccount
            {
                Id = contractor.Id.ToString(),
                AuthUserId = contractor.AuthUserId.ToString(),
                DisplayName = contractor.DisplayName,
                ContactDetails = contractor.ContactDetails,
                ServiceArea = contractor.ServiceArea,
                Trades = trades,
                Active = link.Active,
                AllUnits = link.AllUnits,
                AccessibleUnitIds = accessibleUnitIds,
                DateAdded = AsUtc(link.DateAdded)
            });
        }

        return contractors;
    }

    public async Task RemoveContractorAsync(AuthUser landlord, string contractorId)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        var parsedContractorId = ParseId(contractorId, "contractor");
        var blockers = (await client.From<IssueRow>()
            .Filter("landlord_id", Operator.Equals, ParseId(landlord.Id, "landlord").ToString())
            .Filter("assigned_contractor_id", Operator.Equals, parsedContractorId.ToString())
            .Get()).Models
            .Where(issue => issue.ContractorStatus is "Approved" or "In progress")
            .Select(issue => $"{issue.Title} (unit {issue.UnitNumber})")
            .ToList();
        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(
                $"Reassign these jobs before removing this contractor: {string.Join(", ", blockers)}.");
        }

        await client.Rpc("remove_landlord_contractor", new
        {
            p_contractor_id = parsedContractorId
        });
    }

    public async Task SetLandlordContractorActiveAsync(AuthUser landlord, string contractorId, bool active)
    {
        if (!active)
        {
            await RemoveContractorAsync(landlord, contractorId);
            return;
        }

        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("set_landlord_contractor_active", new
        {
            p_contractor_id = ParseId(contractorId, "contractor"),
            p_active = true
        });
    }

    public async Task<RegistrationInviteLink> CreateContractorInviteAsync(AuthUser landlord, TimeSpan? validity = null)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        var token = CreateToken();
        var expiresAt = GetExpiry(validity ?? TimeSpan.FromDays(30));
        await client.From<ContractorInviteRow>().Insert(new ContractorInviteRow
        {
            Token = token,
            LandlordId = ParseId(landlord.Id, "landlord"),
            ExpiresAt = expiresAt
        });
        return new RegistrationInviteLink { Token = token, ExpiresAt = expiresAt };
    }

    public async Task<RegistrationInviteLink> CreateUnitInviteAsync(AuthUser landlord, string unitId, TimeSpan? validity = null)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        var parsedUnitId = ParseId(unitId, "unit");
        var unit = (await client.From<UnitRow>()
            .Filter("id", Operator.Equals, parsedUnitId.ToString())
            .Get()).Models.FirstOrDefault();
        if (unit is null)
        {
            throw new InvalidOperationException("The unit does not belong to this landlord.");
        }

        var token = CreateToken();
        var expiresAt = GetExpiry(validity);
        await client.From<UnitInviteRow>().Insert(new UnitInviteRow
        {
            Token = token,
            UnitId = parsedUnitId,
            ExpiresAt = expiresAt
        });
        return new RegistrationInviteLink { Token = token, ExpiresAt = expiresAt };
    }

    public async Task<IReadOnlyList<RegistrationInviteLink>> GetUnitInvitesAsync(AuthUser landlord, string unitId)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        var parsedUnitId = ParseId(unitId, "unit");
        var unit = (await client.From<UnitRow>()
            .Filter("id", Operator.Equals, parsedUnitId.ToString())
            .Get()).Models.FirstOrDefault();
        if (unit is null)
        {
            throw new InvalidOperationException("The unit does not belong to this landlord.");
        }

        var now = DateTime.UtcNow;
        var invites = (await client.From<UnitInviteRow>()
            .Filter("unit_id", Operator.Equals, parsedUnitId.ToString())
            .Filter("used", Operator.Equals, "false")
            .Filter("expires_at", Operator.GreaterThan, now.ToString("O"))
            .Order("expires_at", Ordering.Ascending)
            .Get()).Models;
        return invites.Select(invite => new RegistrationInviteLink
        {
            Token = invite.Token,
            ExpiresAt = AsUtc(invite.ExpiresAt)
        }).ToList();
    }

    public async Task RedeemContractorInviteAsync(
        AuthUser contractor,
        string token,
        string displayName,
        IReadOnlyDictionary<string, string> contactDetails,
        string serviceArea,
        IReadOnlyList<string> trades)
    {
        var client = await GetRoleClientAsync(contractor);
        if (string.IsNullOrWhiteSpace(token) ||
            string.IsNullOrWhiteSpace(displayName) ||
            contactDetails is null ||
            trades is null ||
            trades.Count == 0)
        {
            throw new ArgumentException("An invite token, display name, contact details, and at least one trade are required.");
        }

        await client.Rpc("redeem_contractor_invite", new
        {
            p_token = token.Trim(),
            p_display_name = displayName.Trim(),
            p_contact_details = contactDetails,
            p_service_area = serviceArea?.Trim() ?? string.Empty,
            p_trades = trades.Where(trade => !string.IsNullOrWhiteSpace(trade))
                .Select(trade => trade.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
        });
    }

    public async Task LinkContractorInviteAsync(AuthUser contractor, string token)
    {
        var client = await GetContractorAsync(contractor);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("An invite token is required.", nameof(token));
        }

        await client.Client.Rpc("link_contractor_invite", new { p_token = token.Trim() });
    }

    public async Task<IReadOnlyList<LandlordNotification>> GetLandlordNotificationsAsync(AuthUser landlord)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        var rows = (await client.From<LandlordNotificationRow>()
            .Filter("landlord_id", Operator.Equals, ParseId(landlord.Id, "landlord").ToString())
            .Order("created_at", Ordering.Descending)
            .Get()).Models;
        return rows.Select(row => new LandlordNotification
        {
            Id = row.Id.ToString(),
            IssueId = row.IssueId.ToString(),
            Message = row.Message,
            CreatedAt = AsUtc(row.CreatedAt),
            ReadAt = row.ReadAt is null ? null : AsUtc(row.ReadAt.Value)
        }).ToList();
    }

    public async Task MarkNotificationReadAsync(AuthUser landlord, string notificationId)
    {
        var client = await GetRoleClientAsync(landlord, UserRole.Landlord);
        await client.Rpc("mark_landlord_notification_read", new
        {
            p_notification_id = ParseId(notificationId, "notification")
        });
    }

    private async Task<(Supabase.Client Client, ContractorRow Contractor)> GetContractorAsync(AuthUser contractor)
    {
        var client = await GetRoleClientAsync(contractor);
        var contractorId = ParseId(contractor.Id, "contractor user");
        var profile = (await client.From<ContractorRow>()
            .Filter("auth_user_id", Operator.Equals, contractorId.ToString())
            .Get()).Models.FirstOrDefault()
            ?? throw new InvalidOperationException("The signed-in account has no contractor profile.");
        return (client, profile);
    }

    private async Task<Supabase.Client> GetRoleClientAsync(AuthUser user, UserRole? expectedRole = null)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (expectedRole is not null && user.Role != expectedRole)
        {
            throw new InvalidOperationException($"Only a {expectedRole.Value.ToString().ToLowerInvariant()} can perform this contractor action.");
        }

        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentId) ||
            currentId != ParseId(user.Id, "user"))
        {
            throw new InvalidOperationException("The signed-in account does not match the requested Rent Log user.");
        }

        return client;
    }

    private static ContractorJob MapJob(ContractorJobRow row) => new()
    {
        Id = row.IssueId.ToString(),
        IssueId = row.IssueId.ToString(),
        Title = row.Title,
        Description = row.Description,
        LandlordId = row.LandlordId.ToString(),
        LandlordLabel = row.LandlordLabel,
        PropertyName = row.BuildingLabel,
        Suburb = row.Suburb,
        PropertyAddress = row.PropertyAddress,
        UnitNumber = row.UnitNumber ?? string.Empty,
        UnitId = string.Empty,
        AssignedContractorId = row.AssignedContractorId?.ToString(),
        ReopenNote = row.ReopenNote,
        Category = row.Category,
        PreferredWindows = row.PreferredWindows,
        ContractorStatus = ParseContractorStatus(row.ContractorStatus),
        Severity = Enum.TryParse<Severity>(row.Severity, true, out var severity)
            ? severity
            : throw new InvalidDataException($"Unsupported issue severity '{row.Severity}' in the contractor job feed."),
        AcceptedAt = row.AcceptedAt is null ? null : AsUtc(row.AcceptedAt.Value),
        LandlordSeenAt = row.LandlordSeenAt is null ? null : AsUtc(row.LandlordSeenAt.Value),
        WorkDoneAt = row.WorkDoneAt is null ? null : AsUtc(row.WorkDoneAt.Value),
        TenantVerifiedAt = row.TenantVerifiedAt is null ? null : AsUtc(row.TenantVerifiedAt.Value),
        AssignedToMe = row.AssignedToMe,
        ReleasedToContractors = true
    };

    private static Severity ParseSeverity(string severity) =>
        Enum.TryParse<Severity>(severity, true, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Unsupported issue severity '{severity}' in the contractor job feed.");

    private static string? ExtractSuburb(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var separator = address.LastIndexOf(',');
        return separator < 0 || separator == address.Length - 1
            ? null
            : address[(separator + 1)..].Trim();
    }

    internal static ContractorStatus ParseContractorStatus(string status) => status switch
    {
        "Reported" => ContractorStatus.Reported,
        "Open for contractors" => ContractorStatus.OpenForContractors,
        "Pending approval" => ContractorStatus.PendingApproval,
        "Approved" => ContractorStatus.Approved,
        "In progress" => ContractorStatus.InProgress,
        "Work done" => ContractorStatus.WorkDone,
        "Completed" => ContractorStatus.Completed,
        _ => throw new InvalidDataException($"Unsupported contractor status '{status}' in Supabase.")
    };

    private static string ToDatabaseContractorStatus(ContractorStatus status) => status switch
    {
        ContractorStatus.InProgress => "In progress",
        ContractorStatus.WorkDone => "Work done",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static DateTime GetExpiry(TimeSpan? validity)
    {
        var duration = validity ?? TimeSpan.FromDays(7);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromDays(30))
        {
            throw new ArgumentOutOfRangeException(nameof(validity), "Invite validity must be between 1 tick and 30 days.");
        }

        return DateTime.UtcNow.Add(duration);
    }

    private static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static Guid ParseId(string id, string label) =>
        Guid.TryParse(id, out var parsed)
            ? parsed
            : throw new ArgumentException($"The {label} id is not a valid Supabase UUID.", nameof(id));

    private static DateTime AsUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
