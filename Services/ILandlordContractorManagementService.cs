using RentLog.Models;

namespace RentLog.Services;

public interface ILandlordContractorManagementService
{
    Task<IReadOnlyList<ContractorAccount>> GetLandlordContractorsAsync(AuthUser landlord);
    Task SetLandlordContractorActiveAsync(AuthUser landlord, string contractorId, bool active);
    Task RemoveContractorAsync(AuthUser landlord, string contractorId);
    Task<RegistrationInviteLink> CreateContractorInviteAsync(AuthUser landlord, TimeSpan? validity = null);
    Task<RegistrationInviteLink> CreateUnitInviteAsync(AuthUser landlord, string unitId, TimeSpan? validity = null);
    Task<IReadOnlyList<RegistrationInviteLink>> GetUnitInvitesAsync(AuthUser landlord, string unitId);
}
