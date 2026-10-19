using RentLog.Models;

namespace RentLog.Services;

public interface IRentService
{
    Task<IReadOnlyList<RentEntry>> GetRentHistoryAsync(string tenantId);
    Task<IReadOnlyList<RentEntry>> GetLandlordRentEntriesAsync(string landlordId);
    Task<bool> SendReminderAsync(AuthUser landlord, string rentEntryId);
}
