namespace RentLog.Models;

/// <summary>
/// Issue status through its lifecycle.
/// Open → Acknowledged → In progress → Awaiting confirmation → Resolved
/// At any point after Acknowledged, issue can be marked Disputed (returns to comment thread).
/// </summary>
public enum Status
{
    Open,
    Acknowledged,
    InProgress,
    AwaitingConfirmation,
    Resolved,
    Disputed
}
