using RentLog.Models;

namespace RentLog.Services;

public interface IRentService
{
    Task<IReadOnlyList<RentEntry>> GetRentHistoryAsync(string tenantId);
}
