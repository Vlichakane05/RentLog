using RentLog.Models;

namespace RentLog.Services;

public interface IRegistrationService
{
    Task<UnitInvitePreview> GetUnitInvitePreviewAsync(string token);
    Task<ContractorInvitePreview> GetContractorInvitePreviewAsync(string token);
    Task RedeemTenantUnitInviteAsync(AuthUser tenant, string token);
}
