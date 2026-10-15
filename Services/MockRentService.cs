using RentLog.Models;

namespace RentLog.Services;

public class MockRentService : IRentService
{
    private static readonly Dictionary<string, List<RentEntry>> RentHistory = new()
    {
        ["tenant-001"] =
        [
            Entry("rent-2026-10", "tenant-001", 2026, 10, 8500, 2026, 10, 31, null, RentPaymentStatus.Due),
            Entry("rent-2026-09", "tenant-001", 2026, 9, 8500, 2026, 9, 1, new DateOnly(2026, 9, 1), RentPaymentStatus.Paid),
            Entry("rent-2026-08", "tenant-001", 2026, 8, 8500, 2026, 8, 1, new DateOnly(2026, 8, 1), RentPaymentStatus.Paid),
            Entry("rent-2026-07", "tenant-001", 2026, 7, 8500, 2026, 7, 1, new DateOnly(2026, 7, 4), RentPaymentStatus.Paid),
            Entry("rent-2026-06", "tenant-001", 2026, 6, 8500, 2026, 6, 1, new DateOnly(2026, 6, 3), RentPaymentStatus.Paid),
            Entry("rent-2026-05", "tenant-001", 2026, 5, 8500, 2026, 5, 1, null, RentPaymentStatus.Overdue)
        ]
    };

    public Task<IReadOnlyList<RentEntry>> GetRentHistoryAsync(string tenantId)
    {
        IReadOnlyList<RentEntry> entries = RentHistory.TryGetValue(tenantId, out var history)
            ? history.OrderByDescending(entry => entry.Period).ToList()
            : [];
        return Task.FromResult(entries);
    }

    private static RentEntry Entry(string id, string tenantId, int year, int month, decimal amount, int dueYear, int dueMonth, int dueDay, DateOnly? paidDate, RentPaymentStatus status)
    {
        return new RentEntry
        {
            Id = id,
            TenantId = tenantId,
            Period = new DateOnly(year, month, 1),
            Amount = amount,
            DueDate = new DateOnly(dueYear, dueMonth, dueDay),
            PaidDate = paidDate,
            Status = status
        };
    }
}
