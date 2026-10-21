namespace RentLog.Models;

public class RentalProperty
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public List<RentalUnit> Units { get; set; } = [];
}

public class RentalUnit
{
    public string Id { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public decimal MonthlyRent { get; set; }
    public bool IsOccupied { get; set; }
    public List<RentalTenant> Tenants { get; set; } = [];
}

public class RentalTenant
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ContractorJob
{
    public string Id { get; set; } = string.Empty;
    public string IssueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string LandlordId { get; set; } = string.Empty;
    public string LandlordLabel { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string UnitId { get; set; } = string.Empty;
    public string? Suburb { get; set; }
    public string ContractorName { get; set; } = string.Empty;
    public string? AssignedContractorId { get; set; }
    public string? ReopenNote { get; set; }
    public bool ReleasedToContractors { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateOnly? ScheduledDate { get; set; }
    public Status Status { get; set; }
    public Severity Severity { get; set; }
    public ContractorStatus ContractorStatus { get; set; } = ContractorStatus.Reported;
    public string? Category { get; set; }
    public List<string> PreferredWindows { get; set; } = [];
    public string? PropertyAddress { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? LandlordSeenAt { get; set; }
    public DateTime? WorkDoneAt { get; set; }
    public DateTime? TenantVerifiedAt { get; set; }
    public bool AssignedToMe { get; set; }
    public bool HasAssignedContractor { get; set; }
    public bool IsNewForLandlord => LandlordSeenAt is null;
}
