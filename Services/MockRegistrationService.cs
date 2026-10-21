using RentLog.Models;

namespace RentLog.Services;

public sealed class MockRegistrationService : IRegistrationService
{
    private readonly MockLandlordContractorManagementService _contractorManagement;

    public MockRegistrationService(MockLandlordContractorManagementService contractorManagement)
    {
        _contractorManagement = contractorManagement;
    }

    public Task<UnitInvitePreview> GetUnitInvitePreviewAsync(string token) =>
        Task.FromResult(new UnitInvitePreview { Status = "invalid" });

    public Task<ContractorInvitePreview> GetContractorInvitePreviewAsync(string token) =>
        Task.FromResult(_contractorManagement.GetContractorInvitePreview(token));

    public Task RedeemTenantUnitInviteAsync(AuthUser tenant, string token) =>
        Task.FromException(new InvalidOperationException(
            "Invitation registration requires a configured Supabase project."));
}
