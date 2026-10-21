using RentLog.Models;

namespace RentLog.Services;

public interface IContractorService : ILandlordContractorManagementService
{
    Task<ContractorAccount> GetContractorProfileAsync(AuthUser contractor);
    Task UpdateContractorProfileAsync(AuthUser contractor, string serviceArea, IReadOnlyList<string> trades);
    Task<IReadOnlyList<ContractorJob>> GetAvailableJobsAsync(AuthUser contractor);
    Task<IReadOnlyList<ContractorJob>> GetMyJobsAsync(AuthUser contractor);
    Task<IReadOnlyList<ContractorMedia>> GetJobMediaAsync(AuthUser contractor, string issueId);
    Task AddJobUpdateAsync(AuthUser contractor, string issueId, string note, IReadOnlyList<string> mediaPaths);
    Task<IReadOnlyList<ContractorJob>> GetLandlordJobsAsync(AuthUser landlord);
    Task<ContractorJob> AcceptJobAsync(AuthUser contractor, string issueId);
    Task WithdrawJobAsync(AuthUser contractor, string issueId);
    Task SetWorkStatusAsync(AuthUser contractor, string issueId, ContractorStatus status);
    Task VerifyWorkAsync(AuthUser tenant, string issueId);
    Task ReopenWorkAsync(AuthUser tenant, string issueId, string note);
    Task SetJobReleasedAsync(AuthUser landlord, string issueId, bool released);
    Task SetJobDetailsAsync(
        AuthUser landlord,
        string issueId,
        string category,
        IReadOnlyList<string> preferredWindows);
    Task ApproveJobAsync(AuthUser landlord, string issueId);
    Task MarkJobSeenAsync(AuthUser landlord, string issueId);
    Task ReassignJobAsync(AuthUser landlord, string issueId, string contractorId);
    Task RedeemContractorInviteAsync(
        AuthUser contractor,
        string token,
        string displayName,
        IReadOnlyDictionary<string, string> contactDetails,
        string serviceArea,
        IReadOnlyList<string> trades);
    Task LinkContractorInviteAsync(AuthUser contractor, string token);
    Task<IReadOnlyList<LandlordNotification>> GetLandlordNotificationsAsync(AuthUser landlord);
    Task MarkNotificationReadAsync(AuthUser landlord, string notificationId);
}
