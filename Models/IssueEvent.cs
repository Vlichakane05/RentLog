namespace RentLog.Models;

/// <summary>
/// Represents a single event in an issue's timeline (status change, comment, upload).
/// Immutable for audit trail and evidence purposes.
/// </summary>
public class IssueEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// ID of the issue this event belongs to.
    /// </summary>
    public string IssueId { get; set; } = string.Empty;

    /// <summary>
    /// Type of event: StatusChange, Comment, MediaUpload, etc.
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Event description or comment text.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Full name of the user who created this event.
    /// </summary>
    public string AuthorName { get; set; } = string.Empty;

    /// <summary>
    /// Role of the author: Tenant or Landlord.
    /// </summary>
    public string AuthorRole { get; set; } = string.Empty;

    /// <summary>
    /// Status after this event (if StatusChange event).
    /// </summary>
    public Status? NewStatus { get; set; }

    /// <summary>
    /// URLs or file paths of attached media.
    /// </summary>
    public List<string> MediaUrls { get; set; } = new();
}
