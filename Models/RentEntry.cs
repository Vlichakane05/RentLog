namespace RentLog.Models;

public enum RentPaymentStatus
{
    Paid,
    Due,
    Overdue
}

public class RentEntry
{
    public string Id { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public DateOnly Period { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly? PaidDate { get; set; }
    public RentPaymentStatus Status { get; set; }
    public DateTime? LastReminderAt { get; set; }
    public int ReminderCount { get; set; }
}
