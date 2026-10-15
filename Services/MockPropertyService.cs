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
                new RentalUnit { Id = "unit-2a", UnitNumber = "2A", TenantName = "Alex Tenant", TenantId = "tenant-002", MonthlyRent = 9200, IsOccupied = true },
                new RentalUnit { Id = "unit-2b", UnitNumber = "2B", MonthlyRent = 8500, IsOccupied = false },
                new RentalUnit { Id = "unit-3b", UnitNumber = "3B", TenantName = "John Tenant", TenantId = "tenant-001", MonthlyRent = 8500, IsOccupied = true }
            ]
        },
        new RentalProperty
        {
            Id = "property-oak-court",
            Name = "Oak Court",
            Address = "82 Main Road, Rondebosch, Cape Town",
            Units =
            [
                new RentalUnit { Id = "unit-1c", UnitNumber = "1C", TenantName = "Sam Tenant", TenantId = "tenant-003", MonthlyRent = 7800, IsOccupied = true },
                new RentalUnit { Id = "unit-4d", UnitNumber = "4D", TenantName = "Morgan Tenant", TenantId = "tenant-004", MonthlyRent = 8500, IsOccupied = true }
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
                var property = Properties.FirstOrDefault(candidate => candidate.Units.Any(unit => unit.TenantId == issue.TenantId));
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
}
