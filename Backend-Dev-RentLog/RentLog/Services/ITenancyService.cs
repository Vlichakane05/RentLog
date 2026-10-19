using RentLog.Models;

namespace RentLog.Services;

public interface ITenancyService
{
    Task<IReadOnlyList<TenantUnitOption>> GetTenantUnitsAsync(string tenantId);
}
