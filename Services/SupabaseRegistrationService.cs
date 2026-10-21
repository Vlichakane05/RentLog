using RentLog.Models;

namespace RentLog.Services;

public sealed class SupabaseRegistrationService(SupabaseClientProvider clientProvider) : IRegistrationService
{
    public async Task<UnitInvitePreview> GetUnitInvitePreviewAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new UnitInvitePreview { Status = "invalid" };
        }

        var client = await clientProvider.GetClientAsync();
        var result = await client.Rpc<UnitInvitePreviewRow>(
            "preview_unit_invite",
            new { p_token = token.Trim() });
        return result is null
            ? new UnitInvitePreview { Status = "invalid" }
            : new UnitInvitePreview
            {
                Status = result.Status,
                BuildingName = result.BuildingName,
                UnitName = result.UnitName
            };
    }

    public async Task<ContractorInvitePreview> GetContractorInvitePreviewAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ContractorInvitePreview { Status = "invalid" };
        }

        var client = await clientProvider.GetClientAsync();
        var result = await client.Rpc<ContractorInvitePreviewRow>(
            "preview_contractor_invite",
            new { p_token = token.Trim() });
        return result is null
            ? new ContractorInvitePreview { Status = "invalid" }
            : new ContractorInvitePreview
            {
                Status = result.Status,
                LandlordName = result.LandlordName
            };
    }

    public async Task RedeemTenantUnitInviteAsync(AuthUser tenant, string token)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (tenant.Role != UserRole.Tenant)
        {
            throw new InvalidOperationException("Only a tenant account can use a unit invitation.");
        }

        if (!Guid.TryParse(tenant.Id, out var userId))
        {
            throw new ArgumentException("The tenant id is not a valid Supabase UUID.", nameof(tenant));
        }

        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentUserId) || userId != currentUserId)
        {
            throw new InvalidOperationException("The signed-in account does not match this tenant.");
        }

        await client.Rpc("redeem_unit_invite", new { p_token = token.Trim() });
    }
}
