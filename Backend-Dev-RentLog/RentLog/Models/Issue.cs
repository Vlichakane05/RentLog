namespace RentLog.Models;

/// <summary>
/// Represents a maintenance issue logged by a tenant or landlord.
/// </summary>
public class Issue
{
    /// <summary>
    /// Unique identifier for this issue.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Title of the issue (e.g., "Leaking tap in kitchen sink").
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Unit number or identifier where the issue is located.
    /// </summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// Severity level: Low, Medium, High, Emergency.
    /// </summary>
    public Severity Severity { get; set; } = Severity.Low;

    /// <summary>
    /// Current status in the lifecycle.
    /// </summary>
    public Status Status { get; set; } = Status.Open;

    /// <summary>
    /// UTC timestamp when the issue was first logged.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// ID of the tenant who logged the issue.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the landlord or property manager.
    /// </summary>
    public string LandlordId { get; set; } = string.Empty;

    /// <summary>
    /// Timeline of all events (status changes, comments, uploads).
    /// Immutable for evidence purposes.
    /// </summary>
    public List<IssueEvent> Events { get; set; } = new();

    /// <summary>
    /// Whether this issue is currently marked as disputed.
    /// </summary>
    public bool IsDisputed { get; set; }

    public string Description { get; set; } = string.Empty;

    public List<IssueQuote> Quotes { get; set; } = [];

    public string? ContractorName { get; set; }
}
