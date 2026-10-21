using RentLog.Models;

namespace RentLog.Services;

public interface ILandlordContractorJobsService
{
    Task<IReadOnlyList<ContractorJob>> GetLandlordJobsAsync(AuthUser landlord);
    Task<IReadOnlyList<LandlordNotification>> GetLandlordNotificationsAsync(AuthUser landlord);
    Task MarkJobSeenAsync(AuthUser landlord, string issueId);
    Task SetJobReleasedAsync(AuthUser landlord, string issueId, bool released);
    Task SetJobDetailsAsync(AuthUser landlord, string issueId, string category, IReadOnlyList<string> preferredWindows);
    Task ApproveJobAsync(AuthUser landlord, string issueId);
    Task ReturnJobToBoardAsync(AuthUser landlord, string issueId);
    Task ReassignJobAsync(AuthUser landlord, string issueId, string contractorId);
}
