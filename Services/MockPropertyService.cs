using RentLog.Models;

namespace RentLog.Services;

public class MockPropertyService : IPropertyService
{
    private readonly IIssueService _issueService;

    public MockPropertyService(IIssueService issueService)
    {
        _issueService = issueService;
    }

    private static readonly List<RentalProperty> Properties =
    [
        new RentalProperty
        {
            Id = "property-cape-gardens",
            Name = "Cape Gardens",
            Address = "14 Rose Street, Gardens, Cape Town",
            Units =
            [
                new RentalUnit { Id = "unit-2a", UnitNumber = "2A", TenantName = "Alex Tenant", TenantId = "tenant-002", MonthlyRent = 9200, IsOccupied = true, Tenants = [new RentalTenant { Id = "tenant-002", Name = "Alex Tenant" }] },
                new RentalUnit { Id = "unit-2b", UnitNumber = "2B", MonthlyRent = 8500, IsOccupied = false },
                new RentalUnit { Id = "unit-3b", UnitNumber = "3B", TenantName = "John Tenant", TenantId = "tenant-001", MonthlyRent = 8500, IsOccupied = true, Tenants = [new RentalTenant { Id = "tenant-001", Name = "John Tenant" }] }
            ]
        },
        new RentalProperty
        {
            Id = "property-oak-court",
            Name = "Oak Court",
            Address = "82 Main Road, Rondebosch, Cape Town",
            Units =
            [
                new RentalUnit { Id = "unit-1c", UnitNumber = "1C", TenantName = "Sam Tenant", TenantId = "tenant-003", MonthlyRent = 7800, IsOccupied = true, Tenants = [new RentalTenant { Id = "tenant-003", Name = "Sam Tenant" }] },
                new RentalUnit { Id = "unit-4d", UnitNumber = "4D", TenantName = "Morgan Tenant", TenantId = "tenant-004", MonthlyRent = 8500, IsOccupied = true, Tenants = [new RentalTenant { Id = "tenant-004", Name = "Morgan Tenant" }] }
            ]
        }
    ];

    public Task<IReadOnlyList<RentalProperty>> GetPropertiesAsync(string landlordId)
    {
        IReadOnlyList<RentalProperty> properties = landlordId == "landlord-001"
            ? Properties
            : [];
        return Task.FromResult(properties);
    }

    public Task<RentalProperty> CreatePropertyAsync(string landlordId, string name, string address)
    {
        EnsureDemoLandlord(landlordId);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("A building name and address are required.");
        }

        var property = new RentalProperty
        {
            Id = $"property-{Guid.NewGuid():N}",
            Name = name.Trim(),
            Address = address.Trim()
        };
        Properties.Add(property);
        return Task.FromResult(property);
    }

    public Task<RentalUnit> CreateUnitAsync(string landlordId, string propertyId, string unitNumber, decimal monthlyRent)
    {
        EnsureDemoLandlord(landlordId);
        if (string.IsNullOrWhiteSpace(unitNumber) || monthlyRent < 0)
        {
            throw new ArgumentException("Enter a unit number and a non-negative rent amount.");
        }

        var property = Properties.FirstOrDefault(candidate => candidate.Id == propertyId)
            ?? throw new InvalidOperationException("The building was not found.");
        if (property.Units.Any(unit => string.Equals(unit.UnitNumber, unitNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("That unit number already exists in this building.");
        }

        var unit = new RentalUnit
        {
            Id = $"unit-{Guid.NewGuid():N}",
            UnitNumber = unitNumber.Trim(),
            MonthlyRent = monthlyRent
        };
        property.Units.Add(unit);
        return Task.FromResult(unit);
    }

    public Task EndTenantTenancyAsync(string landlordId, string unitId, string tenantId)
    {
        EnsureDemoLandlord(landlordId);
        var unit = Properties.SelectMany(property => property.Units)
            .FirstOrDefault(candidate => candidate.Id == unitId)
            ?? throw new InvalidOperationException("The unit was not found.");
        var tenant = unit.Tenants.FirstOrDefault(candidate => candidate.Id == tenantId)
            ?? throw new InvalidOperationException("The tenant does not have an active tenancy in this unit.");
        unit.Tenants.Remove(tenant);
        unit.TenantName = string.Join(", ", unit.Tenants.Select(candidate => candidate.Name));
        unit.TenantId = unit.Tenants.FirstOrDefault()?.Id ?? string.Empty;
        unit.IsOccupied = unit.Tenants.Count > 0;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ContractorJob>> GetContractorJobsAsync(string landlordId)
    {
        if (landlordId != "landlord-001")
        {
            return [];
        }

        var issues = await _issueService.GetLandlordIssuesAsync(landlordId);
        return issues
            .Where(issue => issue.ContractorName is not null && issue.Status is Status.Acknowledged or Status.InProgress or Status.AwaitingConfirmation)
            .Select(issue =>
            {
                var property = Properties.FirstOrDefault(candidate => candidate.Units.Any(unit =>
                    unit.Tenants.Any(tenant => tenant.Id == issue.TenantId) || unit.TenantId == issue.TenantId));
                return new ContractorJob
                {
                    Id = $"job-{issue.Id}",
                    IssueId = issue.Id,
                    Title = issue.Title,
                    PropertyName = property?.Name ?? "Rental property",
                    UnitNumber = issue.Unit,
                    ContractorName = issue.ContractorName!,
                    ScheduledDate = issue.Id switch
                    {
                        "issue-1001" => new DateOnly(2026, 10, 16),
                        "issue-1006" => new DateOnly(2026, 10, 18),
                        _ => null
                    },
                    Status = issue.Status,
                    Severity = issue.Severity
                };
            })
            .ToList();
    }

    private static void EnsureDemoLandlord(string landlordId)
    {
        if (landlordId != "landlord-001")
        {
            throw new InvalidOperationException("Only the demo landlord can manage properties in demo mode.");
        }
    }
}
