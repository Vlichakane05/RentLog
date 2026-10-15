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
                var tenancy = (await client.From<TenancyRow>()
                    .Filter("unit_id", Operator.Equals, unit.Id.ToString())
                    .Filter("is_active", Operator.Equals, "true")
                    .Get()).Models.FirstOrDefault();
                var tenant = tenancy is null
                    ? null
                    : (await client.From<UserRow>()
                        .Filter("id", Operator.Equals, tenancy.TenantId.ToString())
                        .Get()).Models.FirstOrDefault();

                rentalUnits.Add(new RentalUnit
                {
                    Id = unit.Id.ToString(),
                    UnitNumber = unit.UnitNumber,
                    TenantName = tenant?.FullName ?? string.Empty,
                    TenantId = tenancy?.TenantId.ToString() ?? string.Empty,
                    MonthlyRent = unit.MonthlyRent,
                    IsOccupied = tenancy is not null
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

    public async Task<IReadOnlyList<ContractorJob>> GetContractorJobsAsync(string landlordId)
    {
        var issues = await issueService.GetLandlordIssuesAsync(landlordId);
        var properties = await GetPropertiesAsync(landlordId);
        return issues
            .Where(issue => issue.ContractorName is not null &&
                issue.Status is Status.Acknowledged or Status.InProgress or Status.AwaitingConfirmation)
            .Select(issue =>
            {
                var property = properties.FirstOrDefault(candidate =>
                    candidate.Units.Any(unit => unit.TenantId == issue.TenantId));
                return new ContractorJob
                {
                    Id = $"job-{issue.Id}",
                    IssueId = issue.Id,
                    Title = issue.Title,
                    PropertyName = property?.Name ?? "Rental property",
                    UnitNumber = issue.Unit,
                    ContractorName = issue.ContractorName!,
                    Status = issue.Status,
                    Severity = issue.Severity
                };
            })
            .ToList();
    }
}
