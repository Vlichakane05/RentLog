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
}

public class ContractorJob
{
    public string Id { get; set; } = string.Empty;
    public string IssueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
    public string UnitNumber { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public DateOnly? ScheduledDate { get; set; }
    public Status Status { get; set; }
    public Severity Severity { get; set; }
}
