using RentLog.Models;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public sealed class SupabasePropertyService(
    SupabaseClientProvider clientProvider,
    IIssueService issueService) : IPropertyService
{
    public async Task<IReadOnlyList<RentalProperty>> GetPropertiesAsync(string landlordId)
    {
        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(landlordId, out var parsedLandlordId))
        {
            throw new ArgumentException("The landlord id must be a valid Supabase UUID.", nameof(landlordId));
        }

        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentId) || currentId != parsedLandlordId)
        {
            throw new InvalidOperationException("The requested properties do not belong to the signed-in account.");
        }

        var profile = (await client.From<UserRow>()
            .Filter("id", Operator.Equals, parsedLandlordId.ToString())
            .Get()).Models.FirstOrDefault();
        if (profile?.Role != "landlord")
        {
            throw new InvalidOperationException("Only a landlord account can access property management.");
        }

        var properties = (await client.From<PropertyRow>()
            .Filter("landlord_id", Operator.Equals, parsedLandlordId.ToString())
            .Get()).Models;
        var result = new List<RentalProperty>(properties.Count);

        foreach (var property in properties)
        {
            var units = (await client.From<UnitRow>()
                .Filter("property_id", Operator.Equals, property.Id.ToString())
                .Get()).Models;
            var rentalUnits = new List<RentalUnit>(units.Count);
            foreach (var unit in units)
            {
                var tenancies = (await client.From<TenancyRow>()
                    .Filter("unit_id", Operator.Equals, unit.Id.ToString())
                    .Filter("is_active", Operator.Equals, "true")
                    .Get()).Models;
                var tenants = new List<RentalTenant>(tenancies.Count);
                foreach (var tenancy in tenancies)
                {
                    var tenant = (await client.From<UserRow>()
                        .Filter("id", Operator.Equals, tenancy.TenantId.ToString())
                        .Get()).Models.FirstOrDefault();
                    tenants.Add(new RentalTenant
                    {
                        Id = tenancy.TenantId.ToString(),
                        Name = tenant?.FullName ?? "Tenant"
                    });
                }

                rentalUnits.Add(new RentalUnit
                {
                    Id = unit.Id.ToString(),
                    UnitNumber = unit.UnitNumber,
                    TenantName = string.Join(", ", tenants.Select(tenant => tenant.Name)),
                    TenantId = tenants.FirstOrDefault()?.Id ?? string.Empty,
                    MonthlyRent = unit.MonthlyRent,
                    IsOccupied = tenants.Count > 0,
                    Tenants = tenants
                });
            }

            result.Add(new RentalProperty
            {
                Id = property.Id.ToString(),
                Name = property.Name,
                Address = property.Address,
                Units = rentalUnits
            });
        }

        return result;
    }

    public async Task<RentalProperty> CreatePropertyAsync(string landlordId, string name, string address)
    {
        var client = await GetLandlordClientAsync(landlordId);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A building name and address are required.");
        }

        var response = await client.From<PropertyRow>().Insert(new PropertyRow
        {
            LandlordId = Guid.Parse(landlordId),
            Name = name.Trim(),
            Address = address.Trim()
        });
        var property = response.Models.FirstOrDefault()
            ?? throw new InvalidDataException("Supabase did not return the created building.");
        return new RentalProperty
        {
            Id = property.Id.ToString(),
            Name = property.Name,
            Address = property.Address
        };
    }

    public async Task<RentalUnit> CreateUnitAsync(
        string landlordId,
        string propertyId,
        string unitNumber,
        decimal monthlyRent)
    {
        var client = await GetLandlordClientAsync(landlordId);
        if (string.IsNullOrWhiteSpace(unitNumber))
        {
            throw new ArgumentException("A unit number is required.", nameof(unitNumber));
        }

        if (monthlyRent < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(monthlyRent), "Monthly rent cannot be negative.");
        }

        if (!Guid.TryParse(propertyId, out var parsedPropertyId))
        {
            throw new ArgumentException("The building id must be a valid Supabase UUID.", nameof(propertyId));
        }

        var property = (await client.From<PropertyRow>()
            .Filter("id", Operator.Equals, parsedPropertyId.ToString())
            .Get()).Models.FirstOrDefault();
        if (property is null || property.LandlordId != Guid.Parse(landlordId))
        {
            throw new InvalidOperationException("The building does not belong to this landlord.");
        }

        var response = await client.From<UnitRow>().Insert(new UnitRow
        {
            PropertyId = parsedPropertyId,
            UnitNumber = unitNumber.Trim(),
            MonthlyRent = monthlyRent
        });
        var unit = response.Models.FirstOrDefault()
            ?? throw new InvalidDataException("Supabase did not return the created unit.");
        return new RentalUnit
        {
            Id = unit.Id.ToString(),
            UnitNumber = unit.UnitNumber,
            MonthlyRent = unit.MonthlyRent
        };
    }

    public async Task EndTenantTenancyAsync(string landlordId, string unitId, string tenantId)
    {
        var client = await GetLandlordClientAsync(landlordId);
        await client.Rpc("end_tenant_unit_tenancy", new
        {
            p_unit_id = ParseId(unitId, "unit"),
            p_tenant_id = ParseId(tenantId, "tenant")
        });
    }

    public async Task<IReadOnlyList<ContractorJob>> GetContractorJobsAsync(string landlordId)
    {
        var issues = await issueService.GetLandlordIssuesAsync(landlordId);
        var properties = await GetPropertiesAsync(landlordId);
        var client = await clientProvider.GetClientAsync();
        var jobs = new List<ContractorJob>();
        foreach (var issue in issues.Where(issue =>
            issue.ContractorStatus is ContractorStatus.PendingApproval or ContractorStatus.Approved or
                ContractorStatus.InProgress or ContractorStatus.WorkDone ||
            (issue.ContractorName is not null &&
                issue.Status is Status.Acknowledged or Status.InProgress or Status.AwaitingConfirmation)))
        {
            var contractorName = issue.AssignedContractorId is null
                ? issue.ContractorName
                : (await client.From<ContractorRow>()
                    .Filter("id", Operator.Equals, issue.AssignedContractorId)
                    .Get()).Models.FirstOrDefault()?.DisplayName ?? issue.ContractorName;
            var property = properties.FirstOrDefault(candidate =>
                candidate.Units.Any(unit => unit.Tenants.Any(tenant => tenant.Id == issue.TenantId)
                    || unit.TenantId == issue.TenantId));
            jobs.Add(new ContractorJob
            {
                Id = $"job-{issue.Id}",
                IssueId = issue.Id,
                Title = issue.Title,
                PropertyName = property?.Name ?? "Rental property",
                UnitNumber = issue.Unit,
                ContractorName = contractorName ?? string.Empty,
                Category = issue.Category,
                PreferredWindows = issue.PreferredWindows,
                ContractorStatus = issue.ContractorStatus,
                AcceptedAt = issue.AcceptedAt,
                LandlordSeenAt = issue.LandlordSeenAt,
                WorkDoneAt = issue.WorkDoneAt,
                TenantVerifiedAt = issue.TenantVerifiedAt,
                HasAssignedContractor = issue.AssignedContractorId is not null,
                Status = issue.Status,
                Severity = issue.Severity
            });
        }

        return jobs;
    }

    private async Task<Supabase.Client> GetLandlordClientAsync(string landlordId)
    {
        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(landlordId, out var parsedLandlordId) ||
            !Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentId) ||
            currentId != parsedLandlordId)
        {
            throw new InvalidOperationException("The signed-in account does not match the requested landlord.");
        }

        var profile = (await client.From<UserRow>()
            .Filter("id", Operator.Equals, parsedLandlordId.ToString())
            .Get()).Models.FirstOrDefault();
        if (profile?.Role != "landlord")
        {
            throw new InvalidOperationException("Only a landlord account can manage buildings and units.");
        }

        return client;
    }

    private static Guid ParseId(string id, string label) =>
        Guid.TryParse(id, out var parsed)
            ? parsed
            : throw new ArgumentException($"The {label} id is not a valid Supabase UUID.", nameof(id));
}
