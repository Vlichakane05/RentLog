using RentLog.Models;

namespace RentLog.Services;

public sealed class MockTenancyService : ITenancyService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<TenantUnitOption>> Units =
        new Dictionary<string, IReadOnlyList<TenantUnitOption>>(StringComparer.Ordinal)
        {
            ["tenant-001"] = [new TenantUnitOption { Id = "unit-3b", UnitNumber = "3B", PropertyName = "Cape Gardens" }],
            ["tenant-002"] = [new TenantUnitOption { Id = "unit-2a", UnitNumber = "2A", PropertyName = "Cape Gardens" }],
            ["tenant-003"] = [new TenantUnitOption { Id = "unit-1c", UnitNumber = "1C", PropertyName = "Oak Court" }],
            ["tenant-004"] = [new TenantUnitOption { Id = "unit-4d", UnitNumber = "4D", PropertyName = "Oak Court" }]
        };

    public Task<IReadOnlyList<TenantUnitOption>> GetTenantUnitsAsync(string tenantId)
    {
        return Task.FromResult(Units.TryGetValue(tenantId, out var units)
            ? units
            : (IReadOnlyList<TenantUnitOption>)[]);
    }
}
