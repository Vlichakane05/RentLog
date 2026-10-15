using RentLog.Models;

namespace RentLog.Services;

public interface IIssueService
{
    Task<IReadOnlyList<Issue>> GetTenantIssuesAsync(string tenantId);
    Task<Issue?> GetTenantIssueAsync(string tenantId, string issueId);
    Task<Issue> CreateIssueAsync(AuthUser tenant, string unit, string title, string description, Severity severity, IReadOnlyList<string> mediaUrls);
    Task<Issue?> AddCommentAsync(AuthUser author, string issueId, string comment);
    Task<Issue?> ConfirmResolutionAsync(AuthUser tenant, string issueId);
    Task<Issue?> DisputeResolutionAsync(AuthUser tenant, string issueId, string reason);
}
