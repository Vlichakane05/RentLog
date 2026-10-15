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
        ],
        ["tenant-002"] =
        [
            Entry("rent-2026-10-2a", "tenant-002", 2026, 10, 9200, 2026, 10, 1, null, RentPaymentStatus.Overdue),
            Entry("rent-2026-09-2a", "tenant-002", 2026, 9, 9200, 2026, 9, 1, new DateOnly(2026, 9, 1), RentPaymentStatus.Paid)
        ],
        ["tenant-003"] =
        [
            Entry("rent-2026-10-1c", "tenant-003", 2026, 10, 7800, 2026, 10, 1, null, RentPaymentStatus.Overdue),
            Entry("rent-2026-09-1c", "tenant-003", 2026, 9, 7800, 2026, 9, 1, new DateOnly(2026, 9, 2), RentPaymentStatus.Paid)
        ],
        ["tenant-004"] =
        [
            Entry("rent-2026-10-4d", "tenant-004", 2026, 10, 8500, 2026, 10, 1, null, RentPaymentStatus.Due),
            Entry("rent-2026-09-4d", "tenant-004", 2026, 9, 8500, 2026, 9, 1, new DateOnly(2026, 9, 1), RentPaymentStatus.Paid)
        ]
    };

    private static readonly object _sync = new();

    public Task<IReadOnlyList<RentEntry>> GetRentHistoryAsync(string tenantId)
    {
        IReadOnlyList<RentEntry> entries;
        lock (_sync)
        {
            entries = RentHistory.TryGetValue(tenantId, out var history)
                ? history.OrderByDescending(entry => entry.Period).ToList()
                : [];
        }

        return Task.FromResult(entries);
    }

    public Task<IReadOnlyList<RentEntry>> GetLandlordRentEntriesAsync(string landlordId)
    {
        if (landlordId != "landlord-001")
        {
            return Task.FromResult<IReadOnlyList<RentEntry>>([]);
        }

        IReadOnlyList<RentEntry> entries;
        lock (_sync)
        {
            entries = RentHistory.Values
                .SelectMany(history => history)
                .OrderByDescending(entry => entry.Period)
                .ThenBy(entry => entry.TenantId)
                .ToList();
        }

        return Task.FromResult(entries);
    }

    public Task<bool> SendReminderAsync(AuthUser landlord, string rentEntryId)
    {
        ArgumentNullException.ThrowIfNull(landlord);
        if (landlord.Role != UserRole.Landlord || landlord.Id != "landlord-001")
        {
            throw new InvalidOperationException("Only the assigned landlord can send rent reminders.");
        }

        lock (_sync)
        {
            var entry = RentHistory.Values
                .SelectMany(history => history)
                .FirstOrDefault(item => item.Id == rentEntryId);
            if (entry is null || entry.Status == RentPaymentStatus.Paid)
            {
                return Task.FromResult(false);
            }

            entry.LastReminderAt = DateTime.UtcNow;
            entry.ReminderCount++;
            return Task.FromResult(true);
        }
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
