using RentLog.Models;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public sealed class SupabaseRentService(SupabaseClientProvider clientProvider) : IRentService
{
    public async Task<IReadOnlyList<RentEntry>> GetRentHistoryAsync(string tenantId)
    {
        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(tenantId, out var parsedTenantId))
        {
            throw new ArgumentException("The tenant id must be a valid Supabase UUID.", nameof(tenantId));
        }
        await EnsureCurrentRoleAsync(client, parsedTenantId, "tenant");

        var rows = (await client.From<RentPaymentRow>()
            .Filter("tenant_id", Operator.Equals, parsedTenantId.ToString())
            .Order("period", Ordering.Descending)
            .Get()).Models;
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<RentEntry>> GetLandlordRentEntriesAsync(string landlordId)
    {
        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(landlordId, out var parsedLandlordId))
        {
            throw new ArgumentException("The landlord id must be a valid Supabase UUID.", nameof(landlordId));
        }
        await EnsureCurrentRoleAsync(client, parsedLandlordId, "landlord");

        var units = (await client.From<UnitRow>()
            .Get()).Models;
        var unitIds = units.Select(unit => unit.Id).ToHashSet();
        var tenancies = (await client.From<TenancyRow>()
            .Get()).Models
            .Where(tenancy => unitIds.Contains(tenancy.UnitId))
            .ToDictionary(tenancy => tenancy.Id);
        var rows = (await client.From<RentPaymentRow>()
            .Order("period", Ordering.Descending)
            .Get()).Models;

        return rows
            .Where(row => tenancies.ContainsKey(row.TenancyId))
            .Select(Map)
            .ToList();
    }

    public async Task<bool> SendReminderAsync(AuthUser landlord, string rentEntryId)
    {
        ArgumentNullException.ThrowIfNull(landlord);
        if (landlord.Role != UserRole.Landlord ||
            !Guid.TryParse(landlord.Id, out var landlordId) ||
            !Guid.TryParse(rentEntryId, out var rentId))
        {
            return false;
        }

        var client = await clientProvider.GetClientAsync();
        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentId) || currentId != landlordId)
        {
            throw new InvalidOperationException("The signed-in account does not match the requested landlord.");
        }

        var row = (await client.From<RentPaymentRow>()
            .Filter("id", Operator.Equals, rentId.ToString())
            .Get()).Models.FirstOrDefault();
        if (row is null || row.Status == "paid")
        {
            return false;
        }

        await client.From<RentPaymentRow>()
            .Filter("id", Operator.Equals, rentId.ToString())
            .Set(payment => payment.LastReminderAt!, DateTime.UtcNow)
            .Set(payment => payment.ReminderCount, row.ReminderCount + 1)
            .Update();

        return true;
    }

    private static RentEntry Map(RentPaymentRow row) => new()
    {
        Id = row.Id.ToString(),
        TenantId = row.TenantId.ToString(),
        Period = DateOnly.FromDateTime(row.Period),
        Amount = row.Amount,
        DueDate = DateOnly.FromDateTime(row.DueDate),
        PaidDate = row.PaidDate is null ? null : DateOnly.FromDateTime(row.PaidDate.Value),
        Status = row.Status switch
        {
            "paid" => RentPaymentStatus.Paid,
            "due" => RentPaymentStatus.Due,
            "overdue" => RentPaymentStatus.Overdue,
            _ => throw new InvalidDataException($"Unsupported rent payment status '{row.Status}' in Supabase.")
        },
        LastReminderAt = row.LastReminderAt,
        ReminderCount = row.ReminderCount
    };

    private static async Task EnsureCurrentRoleAsync(Supabase.Client client, Guid userId, string expectedRole)
    {
        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var authenticatedId) || authenticatedId != userId)
        {
            throw new InvalidOperationException("The requested rent history does not belong to the signed-in account.");
        }

        var profile = (await client.From<UserRow>()
            .Filter("id", Operator.Equals, userId.ToString())
            .Get()).Models.FirstOrDefault();
        if (profile?.Role != expectedRole)
        {
            throw new InvalidOperationException($"Only the account's {expectedRole} can access this rent view.");
        }
    }
}
