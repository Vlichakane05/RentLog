namespace RentLog.Models;

public class IssueQuote
{
    public string Id { get; set; } = string.Empty;
    public string ContractorName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string WorkDescription { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileDataUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending approval";
    public string SubmittedBy { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
