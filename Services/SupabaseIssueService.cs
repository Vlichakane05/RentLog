using RentLog.Models;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public sealed class SupabaseIssueService(
    SupabaseClientProvider clientProvider,
    SupabaseMediaService mediaService) : IIssueService
{
    private const string BucketName = "issue-media";

    public async Task<IReadOnlyList<Issue>> GetTenantIssuesAsync(string tenantId) =>
        await GetIssuesAsync("tenant_id", tenantId);

    public Task<Issue?> GetTenantIssueAsync(string tenantId, string issueId) =>
        GetIssueAsync("tenant_id", tenantId, issueId);

    public async Task<IReadOnlyList<Issue>> GetLandlordIssuesAsync(string landlordId) =>
        await GetIssuesAsync("landlord_id", landlordId);

    public Task<Issue?> GetLandlordIssueAsync(string landlordId, string issueId) =>
        GetIssueAsync("landlord_id", landlordId, issueId);

    public async Task<Issue> CreateIssueAsync(
        AuthUser tenant,
        string unit,
        string title,
        string description,
        Severity severity,
        string category,
        IReadOnlyList<string> preferredWindows,
        IReadOnlyList<string> mediaUrls)
    {
        EnsureRole(tenant, UserRole.Tenant);
        if (!IsSupportedWorkCategory(category))
        {
            throw new ArgumentException("Choose a valid work category.", nameof(category));
        }
        if (preferredWindows is null || preferredWindows.Count == 0 ||
            preferredWindows.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Add at least one complete preferred time window.", nameof(preferredWindows));
        }

        var client = await GetAuthorizedClientAsync(tenant);
        var tenantId = ParseId(tenant.Id, "tenant");
        var storagePaths = mediaUrls
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ValidatePendingPath(path, tenantId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var mediaUploads = storagePaths.Select(path =>
        {
            if (!mediaService.TryGetUploadedMetadata(path, out var upload))
            {
                throw new InvalidOperationException("The evidence upload details expired. Upload the files again before submitting the issue.");
            }

            var validationError = upload.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                ? mediaService.ValidatePhoto(upload.MimeType, upload.FileSizeBytes)
                : mediaService.ValidateVideo(upload.MimeType, upload.FileSizeBytes, upload.DurationSeconds);
            if (validationError is not null)
            {
                throw new InvalidOperationException(validationError);
            }

            return (Path: path, Upload: upload);
        }).ToList();
        if (mediaUploads.Count(item => item.Upload.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) > 5 ||
            mediaUploads.Count(item => item.Upload.MimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)) > 2)
        {
            throw new InvalidOperationException("An issue can include up to 5 photos and 2 videos.");
        }

        var tenancies = (await client.From<TenancyRow>()
            .Filter("tenant_id", Operator.Equals, tenantId.ToString())
            .Filter("is_active", Operator.Equals, "true")
            .Get()).Models;

        var requestedUnitId = Guid.TryParse(unit.Trim(), out var parsedUnitId) ? parsedUnitId : (Guid?)null;
        var matchingUnits = new List<UnitRow>();
        foreach (var tenancy in tenancies)
        {
            var possibleUnit = (await client.From<UnitRow>()
                .Filter("id", Operator.Equals, tenancy.UnitId.ToString())
                .Get()).Models.FirstOrDefault();
            if (possibleUnit is not null &&
                (requestedUnitId.HasValue
                    ? possibleUnit.Id == requestedUnitId.Value
                    : string.Equals(possibleUnit.UnitNumber, unit.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                matchingUnits.Add(possibleUnit);
            }
        }

        if (matchingUnits.Count == 0)
        {
            throw new InvalidOperationException("The selected unit is not part of your active tenancy.");
        }
        if (matchingUnits.Count > 1)
        {
            throw new InvalidOperationException("The selected unit is ambiguous. Choose one of your active units and try again.");
        }

        var selectedUnit = matchingUnits[0];

        var issueId = Guid.NewGuid();
        var property = (await client.From<PropertyRow>()
            .Filter("id", Operator.Equals, selectedUnit.PropertyId.ToString())
            .Get()).Models.FirstOrDefault()
            ?? throw new InvalidOperationException("The selected unit is not linked to a managed property.");

        var issue = new IssueRow
        {
            Id = issueId,
            PropertyId = property.Id,
            UnitId = selectedUnit.Id,
            TenantId = tenantId,
            LandlordId = property.LandlordId,
            UnitNumber = selectedUnit.UnitNumber,
            Title = title.Trim(),
            Description = description.Trim(),
            Severity = ToDatabaseValue(severity),
            Status = "open",
            IsDisputed = false,
            Category = category,
            PreferredWindows = preferredWindows.ToList(),
            CreatedAt = DateTime.UtcNow
        };
        await client.From<IssueRow>().Insert(issue);

        if (mediaUploads.Count > 0)
        {
            var records = mediaUploads
                .Select(item => CreateMediaRow(issueId, tenantId, item.Path, item.Upload))
                .ToList();
            await client.From<MediaRow>().Insert(records);
            mediaService.ClearUploadedMetadata(storagePaths);
        }

        return await LoadIssueAsync(client, issueId)
            ?? throw new InvalidOperationException("The new issue could not be loaded after it was saved.");
    }

    public async Task<Issue?> AddCommentAsync(AuthUser author, string issueId, string comment)
    {
        var (client, row) = await GetAuthorizedIssueAsync(author, issueId);
        if (row is null)
        {
            return null;
        }

        await AddEventAsync(client, row.Id, "Comment", comment.Trim());
        return await LoadIssueAsync(client, row.Id);
    }

    public async Task<Issue?> ConfirmResolutionAsync(AuthUser tenant, string issueId)
    {
        EnsureRole(tenant, UserRole.Tenant);
        var (client, row) = await GetAuthorizedIssueAsync(tenant, issueId);
        if (row is null || FromDatabaseStatus(row.Status) != Status.AwaitingConfirmation)
        {
            return null;
        }

        await UpdateStatusAsync(client, row.Id, Status.Resolved);
        return await LoadIssueAsync(client, row.Id);
    }

    public async Task<Issue?> DisputeResolutionAsync(AuthUser tenant, string issueId, string reason)
    {
        EnsureRole(tenant, UserRole.Tenant);
        var (client, row) = await GetAuthorizedIssueAsync(tenant, issueId);
        if (row is null || FromDatabaseStatus(row.Status) != Status.AwaitingConfirmation)
        {
            return null;
        }

        await UpdateStatusAsync(client, row.Id, Status.Disputed);
        await AddEventAsync(client, row.Id, "Comment", reason.Trim());
        return await LoadIssueAsync(client, row.Id);
    }

    public async Task<Issue?> VerifyContractorWorkAsync(AuthUser tenant, string issueId)
    {
        EnsureRole(tenant, UserRole.Tenant);
        var (client, row) = await GetAuthorizedIssueAsync(tenant, issueId);
        if (row is null || row.ContractorStatus != "Work done" || row.TenantVerifiedAt is not null)
        {
            return null;
        }

        await client.Rpc("verify_contractor_work", new { p_issue_id = row.Id });
        return await LoadIssueAsync(client, row.Id);
    }

    public async Task<Issue?> ReopenContractorWorkAsync(AuthUser tenant, string issueId, string note)
    {
        EnsureRole(tenant, UserRole.Tenant);
        if (string.IsNullOrWhiteSpace(note) || note.Trim().Length > 500)
        {
            throw new ArgumentException("Enter a short note of up to 500 characters.", nameof(note));
        }

        var (client, row) = await GetAuthorizedIssueAsync(tenant, issueId);
        if (row is null || row.ContractorStatus != "Work done" || row.TenantVerifiedAt is not null)
        {
            return null;
        }

        await client.Rpc("reopen_contractor_work", new
        {
            p_issue_id = row.Id,
            p_note = note.Trim()
        });
        return await LoadIssueAsync(client, row.Id);
    }

    public async Task<Issue?> AcknowledgeAsync(AuthUser landlord, string issueId)
    {
        EnsureRole(landlord, UserRole.Landlord);
        var (client, row) = await GetAuthorizedIssueAsync(landlord, issueId);
        if (row is null || FromDatabaseStatus(row.Status) != Status.Open)
        {
            return null;
        }

        await UpdateStatusAsync(client, row.Id, Status.Acknowledged);
        return await LoadIssueAsync(client, row.Id);
    }

    public async Task<IssueQuote?> AddQuoteAsync(
        AuthUser landlord,
        string issueId,
        string contractorName,
        decimal amount,
        string workDescription,
        string fileName,
        string contentType,
        byte[] fileBytes)
    {
        EnsureRole(landlord, UserRole.Landlord);
        var (client, issue) = await GetAuthorizedIssueAsync(landlord, issueId);
        if (issue is null || FromDatabaseStatus(issue.Status) is Status.Resolved or Status.Disputed)
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(fileBytes);
        contentType = contentType.ToLowerInvariant();
        if (fileBytes.Length == 0 || fileBytes.Length > 10 * 1024 * 1024)
        {
            throw new InvalidOperationException("The contractor quote must be larger than 0 bytes and no more than 10 MB.");
        }

        if (contentType is not ("application/pdf" or "image/jpeg" or "image/png"))
        {
            throw new InvalidOperationException("Contractor quotes must be PDF, JPG or PNG files.");
        }

        var quoteId = Guid.NewGuid();
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = contentType switch
            {
                "application/pdf" => ".pdf",
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                _ => throw new InvalidOperationException("Unsupported contractor quote type.")
            };
        }

        var storagePath = $"quotes/{issue.Id:D}/{quoteId:N}{extension.ToLowerInvariant()}";
        await client.Storage.From(BucketName).Upload(
            fileBytes,
            storagePath,
            new Supabase.Storage.FileOptions { ContentType = contentType, CacheControl = "3600", Upsert = false });

        var quote = new IssueQuoteRow
        {
            Id = quoteId,
            IssueId = issue.Id,
            ContractorName = contractorName.Trim(),
            Amount = amount,
            WorkDescription = workDescription.Trim(),
            FileName = Path.GetFileName(fileName),
            StoragePath = storagePath,
            Status = "pending_approval",
            SubmittedBy = ParseId(landlord.Id, "landlord"),
            SubmittedAt = DateTime.UtcNow
        };
        await client.From<IssueQuoteRow>().Insert(quote);

        return await MapQuoteAsync(client, quote);
    }

    public async Task<Issue?> ApproveQuoteAsync(AuthUser landlord, string issueId, string quoteId)
    {
        EnsureRole(landlord, UserRole.Landlord);
        var (client, issue) = await GetAuthorizedIssueAsync(landlord, issueId);
        if (issue is null || !Guid.TryParse(quoteId, out var parsedQuoteId))
        {
            return null;
        }

        var quote = (await client.From<IssueQuoteRow>()
            .Filter("id", Operator.Equals, parsedQuoteId.ToString())
            .Filter("issue_id", Operator.Equals, issue.Id.ToString())
            .Get()).Models.FirstOrDefault();
        if (quote is null || quote.Status != "pending_approval")
        {
            return null;
        }

        await client.From<IssueQuoteRow>()
            .Filter("id", Operator.Equals, quote.Id.ToString())
            .Set(row => row.Status, "approved")
            .Update();

        return await LoadIssueAsync(client, issue.Id);
    }

    public async Task<Issue?> MarkFixedAsync(AuthUser landlord, string issueId, string comment)
    {
        EnsureRole(landlord, UserRole.Landlord);
        var (client, row) = await GetAuthorizedIssueAsync(landlord, issueId);
        if (row is null || FromDatabaseStatus(row.Status) is Status.Open or Status.Resolved or Status.Disputed)
        {
            return null;
        }

        await AddEventAsync(client, row.Id, "Comment", comment.Trim());
        await UpdateStatusAsync(client, row.Id, Status.AwaitingConfirmation);
        return await LoadIssueAsync(client, row.Id);
    }

    public async Task<Issue?> RespondToDisputeAsync(AuthUser landlord, string issueId, string response, bool agreeResolved)
    {
        EnsureRole(landlord, UserRole.Landlord);
        var (client, row) = await GetAuthorizedIssueAsync(landlord, issueId);
        if (row is null || FromDatabaseStatus(row.Status) != Status.Disputed)
        {
            return null;
        }

        await AddEventAsync(client, row.Id, "Comment", response.Trim());
        await UpdateStatusAsync(client, row.Id, agreeResolved ? Status.AwaitingConfirmation : Status.InProgress);
        return await LoadIssueAsync(client, row.Id);
    }

    private async Task<IReadOnlyList<Issue>> GetIssuesAsync(string ownerColumn, string ownerId)
    {
        var client = await clientProvider.GetClientAsync();
        var owner = ParseId(ownerId, ownerColumn);
        var rows = (await client.From<IssueRow>()
            .Filter(ownerColumn, Operator.Equals, owner.ToString())
            .Order("created_at", Ordering.Descending)
            .Get()).Models;
        var issues = new List<Issue>(rows.Count);
        foreach (var row in rows)
        {
            var issue = await LoadIssueAsync(client, row.Id);
            if (issue is not null)
            {
                issues.Add(issue);
            }
        }

        return issues;
    }

    private async Task<Issue?> GetIssueAsync(string ownerColumn, string ownerId, string issueId)
    {
        if (!Guid.TryParse(ownerId, out var owner) || !Guid.TryParse(issueId, out var parsedIssueId))
        {
            return null;
        }

        var client = await clientProvider.GetClientAsync();
        var row = (await client.From<IssueRow>()
            .Filter(ownerColumn, Operator.Equals, owner.ToString())
            .Filter("id", Operator.Equals, parsedIssueId.ToString())
            .Get()).Models.FirstOrDefault();
        return row is null ? null : await LoadIssueAsync(client, row.Id);
    }

    private async Task<(Supabase.Client Client, IssueRow? Issue)> GetAuthorizedIssueAsync(AuthUser author, string issueId)
    {
        ArgumentNullException.ThrowIfNull(author);
        var client = await GetAuthorizedClientAsync(author);
        if (!Guid.TryParse(issueId, out var parsedIssueId))
        {
            return (client, null);
        }

        var column = author.Role == UserRole.Tenant ? "tenant_id" : "landlord_id";
        var row = (await client.From<IssueRow>()
            .Filter(column, Operator.Equals, ParseId(author.Id, author.Role.ToString()).ToString())
            .Filter("id", Operator.Equals, parsedIssueId.ToString())
            .Get()).Models.FirstOrDefault();
        return (client, row);
    }

    private async Task<Supabase.Client> GetAuthorizedClientAsync(AuthUser user)
    {
        var client = await clientProvider.GetClientAsync();
        var authenticatedId = client.Auth.CurrentUser?.Id;
        if (!Guid.TryParse(authenticatedId, out var currentId) ||
            currentId != ParseId(user.Id, user.Role.ToString()))
        {
            throw new InvalidOperationException("The signed-in account does not match the requested Rent Log user.");
        }

        return client;
    }

    private async Task<Issue?> LoadIssueAsync(Supabase.Client client, Guid issueId)
    {
        var row = (await client.From<IssueRow>()
            .Filter("id", Operator.Equals, issueId.ToString())
            .Get()).Models.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        var events = (await client.From<IssueEventRow>()
            .Filter("issue_id", Operator.Equals, issueId.ToString())
            .Order("created_at", Ordering.Ascending)
            .Get()).Models;
        var media = (await client.From<MediaRow>()
            .Filter("issue_id", Operator.Equals, issueId.ToString())
            .Get()).Models;
        var mappedEvents = new List<IssueEvent>(events.Count);
        foreach (var item in events)
        {
            var urls = new List<string>();
            foreach (var file in media.Where(record => record.EventId == item.Id))
            {
                urls.Add(await client.Storage.From(BucketName).CreateSignedUrl(file.StoragePath, 3600));
            }

            mappedEvents.Add(new IssueEvent
            {
                Id = item.Id.ToString(),
                IssueId = item.IssueId.ToString(),
                EventType = item.EventType,
                Description = item.Description,
                Timestamp = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc),
                AuthorName = item.AuthorName,
                AuthorRole = item.AuthorRole,
                NewStatus = item.NewStatus is null ? null : FromDatabaseStatus(item.NewStatus),
                MediaUrls = urls
            });
        }

        var quoteRows = (await client.From<IssueQuoteRow>()
            .Filter("issue_id", Operator.Equals, issueId.ToString())
            .Order("submitted_at", Ordering.Ascending)
            .Get()).Models;
        var quotes = new List<IssueQuote>(quoteRows.Count);
        foreach (var quote in quoteRows)
        {
            quotes.Add(await MapQuoteAsync(client, quote));
        }

        var contractorFirstName = string.Empty;
        var contractorTrade = string.Empty;
        if (row.AssignedContractorId is not null &&
            string.Equals(client.Auth.CurrentUser?.Id, row.TenantId.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var contractorDetails = await client.Rpc<TenantIssueContractorDetailsRow>(
                "get_tenant_issue_contractor_details",
                new { p_issue_id = row.Id });
            contractorFirstName = contractorDetails?.ContractorFirstName ?? string.Empty;
            contractorTrade = contractorDetails?.ContractorTrade ?? string.Empty;
        }

        return new Issue
        {
            Id = row.Id.ToString(),
            Title = row.Title,
            Unit = row.UnitNumber,
            Severity = FromDatabaseSeverity(row.Severity),
            Status = FromDatabaseStatus(row.Status),
            CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc),
            TenantId = row.TenantId.ToString(),
            LandlordId = row.LandlordId.ToString(),
            IsDisputed = row.IsDisputed,
            Description = row.Description,
            ContractorName = row.ContractorName,
            ContractorFirstName = contractorFirstName,
            ContractorTrade = contractorTrade,
            Category = row.Category,
            PreferredWindows = row.PreferredWindows,
            ReleasedToContractors = row.ReleasedToContractors,
            AssignedContractorId = row.AssignedContractorId?.ToString(),
            ContractorStatus = SupabaseContractorService.ParseContractorStatus(row.ContractorStatus),
            AcceptedAt = row.AcceptedAt is null ? null : DateTime.SpecifyKind(row.AcceptedAt.Value, DateTimeKind.Utc),
            LandlordSeenAt = row.LandlordSeenAt is null ? null : DateTime.SpecifyKind(row.LandlordSeenAt.Value, DateTimeKind.Utc),
            WorkDoneAt = row.WorkDoneAt is null ? null : DateTime.SpecifyKind(row.WorkDoneAt.Value, DateTimeKind.Utc),
            TenantVerifiedAt = row.TenantVerifiedAt is null ? null : DateTime.SpecifyKind(row.TenantVerifiedAt.Value, DateTimeKind.Utc),
            ReopenNote = row.ReopenNote,
            Events = mappedEvents,
            Quotes = quotes
        };
    }

    private async Task<IssueQuote> MapQuoteAsync(Supabase.Client client, IssueQuoteRow quote)
    {
        var signedUrl = await client.Storage.From(BucketName).CreateSignedUrl(quote.StoragePath, 3600);
        var author = (await client.From<UserRow>()
            .Filter("id", Operator.Equals, quote.SubmittedBy.ToString())
            .Get()).Models.FirstOrDefault();
        return new IssueQuote
        {
            Id = quote.Id.ToString(),
            ContractorName = quote.ContractorName,
            Amount = quote.Amount,
            WorkDescription = quote.WorkDescription,
            FileName = quote.FileName,
            FileDataUrl = signedUrl,
            Status = quote.Status == "approved" ? "Approved" : "Pending approval",
            SubmittedBy = author?.FullName ?? string.Empty,
            SubmittedAt = DateTime.SpecifyKind(quote.SubmittedAt, DateTimeKind.Utc)
        };
    }

    private static async Task<Guid> AddEventAsync(Supabase.Client client, Guid issueId, string eventType, string description)
    {
        var currentId = client.Auth.CurrentUser?.Id;
        if (!Guid.TryParse(currentId, out var authorId))
        {
            throw new InvalidOperationException("Sign in before adding to an issue's history.");
        }

        var eventId = Guid.NewGuid();
        await client.From<IssueEventRow>().Insert(new IssueEventRow
        {
            Id = eventId,
            IssueId = issueId,
            AuthorId = authorId,
            EventType = eventType,
            Description = description.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        return eventId;
    }

    private static MediaRow CreateMediaRow(
        Guid issueId,
        Guid uploadedBy,
        string storagePath,
        UploadedMediaMetadata metadata)
    {
        var fileName = Path.GetFileName(storagePath);
        return new MediaRow
        {
            Id = Guid.NewGuid(),
            IssueId = issueId,
            EventId = null,
            UploadedBy = uploadedBy,
            StoragePath = storagePath,
            FileName = metadata.FileName,
            MimeType = metadata.MimeType,
            FileSizeBytes = metadata.FileSizeBytes,
            AltText = metadata.AltText,
            DurationSeconds = metadata.DurationSeconds,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string ValidatePendingPath(string path, Guid tenantId)
    {
        var normalized = path.Replace('\\', '/').Trim('/');
        var expectedPrefix = $"pending/{tenantId:D}/";
        if (!normalized.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase) ||
            normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException("The selected evidence upload does not belong to this account.");
        }

        return normalized;
    }

    private static async Task UpdateStatusAsync(Supabase.Client client, Guid issueId, Status status)
    {
        await client.From<IssueRow>()
            .Filter("id", Operator.Equals, issueId.ToString())
            .Set(row => row.Status, ToDatabaseValue(status))
            .Update();
    }

    private static void EnsureRole(AuthUser user, UserRole role)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Role != role)
        {
            throw new InvalidOperationException($"Only a {role.ToString().ToLowerInvariant()} can perform this issue action.");
        }
    }

    private static Guid ParseId(string id, string label) =>
        Guid.TryParse(id, out var parsed)
            ? parsed
            : throw new ArgumentException($"The {label} id is not a valid Supabase UUID.", nameof(id));

    private static string ToDatabaseValue(Enum value) =>
        string.Concat(value.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"_{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    private static bool IsSupportedWorkCategory(string? category) => category is
        "Plumbing" or "Electrical" or "Appliance" or "Locks and Security" or "Pest Control" or "General Maintenance";

    private static Status FromDatabaseStatus(string status) => status switch
    {
        "in_progress" => Status.InProgress,
        "awaiting_confirmation" => Status.AwaitingConfirmation,
        _ when Enum.TryParse<Status>(status, true, out var parsed) => parsed,
        _ => throw new InvalidDataException($"Unsupported issue status '{status}' in Supabase.")
    };

    private static Severity FromDatabaseSeverity(string severity) =>
        Enum.TryParse<Severity>(severity, true, out var parsed)
            ? parsed
            : throw new InvalidDataException($"Unsupported issue severity '{severity}' in Supabase.");
}
