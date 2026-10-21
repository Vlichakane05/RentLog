namespace RentLog.Models;

public enum ContractorStatus
{
    Reported,
    OpenForContractors,
    PendingApproval,
    Approved,
    InProgress,
    WorkDone,
    Completed
}

public class ContractorAccount
{
    public string Id { get; set; } = string.Empty;
    public string AuthUserId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public Dictionary<string, string> ContactDetails { get; set; } = [];
    public string ServiceArea { get; set; } = string.Empty;
    public List<string> Trades { get; set; } = [];
    public bool Active { get; set; }
    public bool AllUnits { get; set; }
    public List<string> AccessibleUnitIds { get; set; } = [];
    public DateTime DateAdded { get; set; }
}

public class LandlordNotification
{
    public string Id { get; set; } = string.Empty;
    public string IssueId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class ContractorMedia
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string SignedUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
