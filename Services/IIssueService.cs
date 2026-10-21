using RentLog.Models;

namespace RentLog.Services;

public interface IIssueService
{
    Task<IReadOnlyList<Issue>> GetTenantIssuesAsync(string tenantId);
    Task<Issue?> GetTenantIssueAsync(string tenantId, string issueId);
    Task<IReadOnlyList<Issue>> GetLandlordIssuesAsync(string landlordId);
    Task<Issue?> GetLandlordIssueAsync(string landlordId, string issueId);
    Task<Issue> CreateIssueAsync(
        AuthUser tenant,
        string unit,
        string title,
        string description,
        Severity severity,
        string category,
        IReadOnlyList<string> preferredWindows,
        IReadOnlyList<string> mediaUrls);
    Task<Issue?> AddCommentAsync(AuthUser author, string issueId, string comment);
    Task<Issue?> ConfirmResolutionAsync(AuthUser tenant, string issueId);
    Task<Issue?> DisputeResolutionAsync(AuthUser tenant, string issueId, string reason);
    Task<Issue?> VerifyContractorWorkAsync(AuthUser tenant, string issueId);
    Task<Issue?> ReopenContractorWorkAsync(AuthUser tenant, string issueId, string note);
    Task<Issue?> AcknowledgeAsync(AuthUser landlord, string issueId);
    Task<IssueQuote?> AddQuoteAsync(AuthUser landlord, string issueId, string contractorName, decimal amount, string workDescription, string fileName, string contentType, byte[] fileBytes);
    Task<Issue?> ApproveQuoteAsync(AuthUser landlord, string issueId, string quoteId);
    Task<Issue?> MarkFixedAsync(AuthUser landlord, string issueId, string comment);
    Task<Issue?> RespondToDisputeAsync(AuthUser landlord, string issueId, string response, bool agreeResolved);
}
