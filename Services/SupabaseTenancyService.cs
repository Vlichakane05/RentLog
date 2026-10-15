using RentLog.Models;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public sealed class SupabaseTenancyService(SupabaseClientProvider clientProvider) : ITenancyService
{
    public async Task<IReadOnlyList<TenantUnitOption>> GetTenantUnitsAsync(string tenantId)
    {
        if (!Guid.TryParse(tenantId, out var parsedTenantId))
        {
            throw new ArgumentException("The tenant id must be a valid Supabase UUID.", nameof(tenantId));
        }

        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentUserId) ||
            currentUserId != parsedTenantId)
        {
            throw new InvalidOperationException("The requested tenancy does not belong to the signed-in account.");
        }

        var profile = (await client.From<UserRow>()
            .Filter("id", Operator.Equals, parsedTenantId.ToString())
            .Get()).Models.FirstOrDefault();
        if (profile?.Role != "tenant")
        {
            throw new InvalidOperationException("Only a tenant account can view active tenancy units.");
        }

        var tenancies = (await client.From<TenancyRow>()
            .Filter("tenant_id", Operator.Equals, parsedTenantId.ToString())
            .Filter("is_active", Operator.Equals, "true")
            .Get()).Models;
        var units = new List<TenantUnitOption>(tenancies.Count);
        foreach (var tenancy in tenancies)
        {
            var unit = (await client.From<UnitRow>()
                .Filter("id", Operator.Equals, tenancy.UnitId.ToString())
                .Get()).Models.FirstOrDefault();
            if (unit is null)
            {
                continue;
            }

            var property = (await client.From<PropertyRow>()
                .Filter("id", Operator.Equals, unit.PropertyId.ToString())
                .Get()).Models.FirstOrDefault();
            if (property is null)
            {
                continue;
            }

            units.Add(new TenantUnitOption
            {
                Id = unit.Id.ToString(),
                UnitNumber = unit.UnitNumber,
                PropertyName = property.Name
            });
        }

        return units
            .OrderBy(unit => unit.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(unit => unit.UnitNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
