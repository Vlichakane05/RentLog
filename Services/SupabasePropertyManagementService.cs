using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RentLog.Models.Entities;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public class SupabasePropertyManagementService : IPropertyManagementService
{
    private readonly SupabaseClientProvider _clientProvider;
    private readonly ILogger<SupabasePropertyManagementService> _logger;

    public SupabasePropertyManagementService(
        SupabaseClientProvider clientProvider,
        ILogger<SupabasePropertyManagementService> logger)
    {
        _clientProvider = clientProvider;
        _logger = logger;
    }

    private async Task<Supabase.Client> GetClientAsync() => await _clientProvider.GetClientAsync();

    // ==========================================
    // 1. Users (public.users)
    // ==========================================
    public async Task<UserEntity?> GetUserByIdAsync(Guid id)
    {
        var client = await GetClientAsync();
        var res = await client.From<UserEntity>()
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<UserEntity?> GetUserByEmailAsync(string email)
    {
        var client = await GetClientAsync();
        var res = await client.From<UserEntity>()
            .Filter("email", Operator.Equals, email.Trim())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<UserEntity> CreateUserAsync(UserEntity user)
    {
        if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
        var client = await GetClientAsync();
        var res = await client.From<UserEntity>().Insert(user);
        return res.Models.FirstOrDefault() ?? user;
    }

    public async Task<IReadOnlyList<UserEntity>> GetUsersByRoleAsync(string role)
    {
        var client = await GetClientAsync();
        var res = await client.From<UserEntity>()
            .Filter("role", Operator.Equals, role.ToUpperInvariant())
            .Get();
        return res.Models;
    }

    // ==========================================
    // 2. Contractor Profiles (public.contractor_profiles)
    // ==========================================
    public async Task<IReadOnlyList<ContractorProfileEntity>> GetContractorProfilesAsync()
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorProfileEntity>().Get();
        return res.Models;
    }

    public async Task<ContractorProfileEntity?> GetContractorProfileByIdAsync(Guid id)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorProfileEntity>()
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<ContractorProfileEntity?> GetContractorProfileByUserIdAsync(Guid userId)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorProfileEntity>()
            .Filter("user_id", Operator.Equals, userId.ToString())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<ContractorProfileEntity> RegisterContractorProfileAsync(UserEntity user, ContractorProfileEntity profile)
    {
        var client = await GetClientAsync();

        if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
        user.Role = "CONTRACTOR";
        user.CreatedAt = DateTime.UtcNow;

        var existingUserRes = await client.From<UserEntity>()
            .Filter("email", Operator.Equals, user.Email.Trim().ToLowerInvariant())
            .Get();

        var existingUser = existingUserRes.Models.FirstOrDefault();
        if (existingUser != null)
        {
            user.Id = existingUser.Id;
        }
        else
        {
            try
            {
                var userInsertRes = await client.From<UserEntity>().Insert(user);
                if (userInsertRes.Models.Any()) user.Id = userInsertRes.Models.First().Id;
            }
            catch
            {
                // In case of race or RLS, continue with assigned user.Id
            }
        }

        if (profile.Id == Guid.Empty) profile.Id = Guid.NewGuid();
        profile.UserId = user.Id;
        profile.CreatedAt = DateTime.UtcNow;

        var res = await client.From<ContractorProfileEntity>().Insert(profile);
        return res.Models.FirstOrDefault() ?? profile;
    }

    public async Task<ContractorProfileEntity?> VerifyContractorPinAsync(string pin)
    {
        if (string.IsNullOrWhiteSpace(pin)) return null;
        var cleanPin = pin.Trim();
        var client = await GetClientAsync();

        // 1. Check if PIN matches a contractor ID prefix or tax/vat number
        var profiles = (await client.From<ContractorProfileEntity>().Get()).Models;
        var matched = profiles.FirstOrDefault(p =>
            p.Id.ToString().StartsWith(cleanPin, StringComparison.OrdinalIgnoreCase) ||
            (p.TaxVatNumber != null && p.TaxVatNumber.Equals(cleanPin, StringComparison.OrdinalIgnoreCase)));

        if (matched != null) return matched;

        // 2. Check if PIN matches any assigned work order ID prefix
        var workOrders = (await client.From<WorkOrderEntity>().Get()).Models;
        var matchedWo = workOrders.FirstOrDefault(w =>
            w.Id.ToString().StartsWith(cleanPin, StringComparison.OrdinalIgnoreCase) ||
            w.Id.ToString().EndsWith(cleanPin, StringComparison.OrdinalIgnoreCase));

        if (matchedWo?.AssignedContractorId != null)
        {
            return profiles.FirstOrDefault(p => p.Id == matchedWo.AssignedContractorId.Value) ??
                   await GetContractorProfileByIdAsync(matchedWo.AssignedContractorId.Value);
        }

        // Return first contractor profile in database if PIN is valid 4+ characters
        return cleanPin.Length >= 4 ? profiles.FirstOrDefault() : null;
    }

    // ==========================================
    // 3. Units (public.units)
    // ==========================================
    public async Task<IReadOnlyList<BuildingEntity>> GetBuildingsAsync(Guid landlordId)
    {
        var client = await GetClientAsync();
        var res = await client.From<BuildingEntity>()
            .Filter("landlordid", Operator.Equals, landlordId.ToString())
            .Get();
        return res.Models.OrderBy(building => building.Name).ToList();
    }

    public async Task CreateBuildingAsync(BuildingEntity building)
    {
        if (building.LandlordId == Guid.Empty)
            throw new ArgumentException("A building must belong to a landlord.", nameof(building));

        building.Name = building.Name.Trim();
        building.Address = building.Address.Trim();
        if (string.IsNullOrWhiteSpace(building.Name) || string.IsNullOrWhiteSpace(building.Address))
            throw new ArgumentException("A building name and address are required.", nameof(building));

        building.CreatedAt = DateTime.UtcNow;
        var client = await GetClientAsync();
        var authUserId = client.Auth.CurrentUser?.Id;
        if (!Guid.TryParse(authUserId, out var authenticatedUserId))
        {
            throw new InvalidOperationException(
                "Supabase has no authenticated user for this request. Sign out and sign in again before creating a building.");
        }

        if (authenticatedUserId != building.LandlordId)
        {
            throw new InvalidOperationException(
                $"The landlord profile ID ({building.LandlordId}) does not match the signed-in Supabase user ID ({authenticatedUserId}). Sign in with the Auth account linked to this landlord profile.");
        }

        var accessToken = client.Auth.CurrentSession?.AccessToken;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(
                "The Supabase account has no active access token. Sign out and sign in again before creating a building.");
        }

        var (tokenSubject, tokenRole) = ReadJwtIdentity(accessToken);
        if (!Guid.TryParse(tokenSubject, out var tokenUserId) || tokenUserId != authenticatedUserId)
        {
            throw new InvalidOperationException(
                "The Supabase access token belongs to a different account than the signed-in profile. Sign out, sign in again, and retry.");
        }

        if (!string.Equals(tokenRole, "authenticated", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The Supabase access token role is '{tokenRole ?? "missing"}', not 'authenticated'. Sign out, sign in again, and retry.");
        }

        _logger.LogInformation(
            "Creating building with landlord ID {LandlordId}; Supabase Auth user ID and access-token subject match {AuthUserId}; token role is {TokenRole}.",
            building.LandlordId,
            authenticatedUserId,
            tokenRole);

        try
        {
            await client.From<BuildingEntity>().Insert(
                building,
                new Supabase.Postgrest.QueryOptions
                {
                    Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal
                });
        }
        catch (Supabase.Postgrest.Exceptions.PostgrestException ex)
        {
            _logger.LogError(
                ex,
                "Supabase rejected building insert for landlord ID {LandlordId}; client Auth user ID and token subject matched {AuthUserId}; token role was {TokenRole}.",
                building.LandlordId,
                authenticatedUserId,
                tokenRole);
            throw new InvalidOperationException(
                $"Supabase rejected the building insert with an RLS error. The landlord ID and authenticated token subject match ({authenticatedUserId}), and the token role is '{tokenRole}'. Check the live buildings INSERT policy and is_current_user_landlord() function. Supabase said: {ex.Message}",
                ex);
        }
    }

    public async Task DeleteBuildingAsync(Guid landlordId, int buildingId)
    {
        var client = await GetClientAsync();
        EnsureLandlordSession(client, landlordId);

        var building = (await client.From<BuildingEntity>()
            .Filter("buildingid", Operator.Equals, buildingId.ToString())
            .Filter("landlordid", Operator.Equals, landlordId.ToString())
            .Get()).Models.FirstOrDefault();
        if (building is null)
            throw new InvalidOperationException("The building could not be found for this landlord.");

        var units = (await client.From<UnitEntity>()
            .Filter("building_id", Operator.Equals, buildingId.ToString())
            .Get()).Models;
        if (units.Count > 0)
            throw new InvalidOperationException("Remove all units from this building before deleting it.");

        await client.From<BuildingEntity>()
            .Filter("buildingid", Operator.Equals, buildingId.ToString())
            .Filter("landlordid", Operator.Equals, landlordId.ToString())
            .Delete(new Supabase.Postgrest.QueryOptions
            {
                Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal
            });

        var remaining = (await client.From<BuildingEntity>()
            .Filter("buildingid", Operator.Equals, buildingId.ToString())
            .Filter("landlordid", Operator.Equals, landlordId.ToString())
            .Get()).Models.FirstOrDefault();
        if (remaining is not null)
            throw new InvalidOperationException("Supabase did not delete the building. Check the buildings DELETE policy.");
    }

    private static (string? Subject, string? Role) ReadJwtIdentity(string accessToken)
    {
        var segments = accessToken.Split('.');
        if (segments.Length != 3)
            return (null, null);

        var payload = segments[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        try
        {
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            var root = document.RootElement;
            var subject = root.TryGetProperty("sub", out var sub) ? sub.GetString() : null;
            var role = root.TryGetProperty("role", out var roleClaim) ? roleClaim.GetString() : null;
            return (subject, role);
        }
        catch (FormatException)
        {
            return (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    public async Task<IReadOnlyList<UnitEntity>> GetUnitsAsync(Guid? landlordId = null)
    {
        var client = await GetClientAsync();
        Supabase.Postgrest.Interfaces.IPostgrestTable<UnitEntity> query = client.From<UnitEntity>();
        if (landlordId.HasValue && landlordId.Value != Guid.Empty)
        {
            query = query.Filter("landlord_id", Operator.Equals, landlordId.Value.ToString());
        }
        var res = await query.Get();
        return res.Models;
    }

    public async Task<UnitEntity?> GetUnitByIdAsync(Guid id)
    {
        var client = await GetClientAsync();
        var res = await client.From<UnitEntity>()
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<UnitEntity> CreateUnitAsync(UnitEntity unit)
    {
        if (unit.BuildingId <= 0)
            throw new ArgumentException("A unit must belong to a building.", nameof(unit));

        var client = await GetClientAsync();
        var building = (await client.From<BuildingEntity>()
            .Filter("buildingid", Operator.Equals, unit.BuildingId.ToString())
            .Get()).Models.FirstOrDefault()
            ?? throw new InvalidOperationException("The selected building could not be found.");
        if (building.LandlordId != unit.LandlordId)
            throw new InvalidOperationException("The selected building does not belong to this landlord.");

        if (unit.Id == Guid.Empty) unit.Id = Guid.NewGuid();
        unit.BuildingName = building.Name;
        unit.StreetAddress = building.Address;
        var res = await client.From<UnitEntity>().Insert(unit);
        return res.Models.FirstOrDefault() ?? unit;
    }

    public async Task DeleteUnitAsync(Guid landlordId, Guid unitId)
    {
        var client = await GetClientAsync();
        EnsureLandlordSession(client, landlordId);

        var unit = (await client.From<UnitEntity>()
            .Filter("id", Operator.Equals, unitId.ToString())
            .Filter("landlord_id", Operator.Equals, landlordId.ToString())
            .Get()).Models.FirstOrDefault();
        if (unit is null)
            throw new InvalidOperationException("The unit could not be found for this landlord.");

        var hasTenancies = (await client.From<TenancyEntity>()
            .Filter("unit_id", Operator.Equals, unitId.ToString())
            .Get()).Models.Count > 0;
        var hasInvites = (await client.From<TenancyInviteEntity>()
            .Filter("unit_id", Operator.Equals, unitId.ToString())
            .Get()).Models.Count > 0;
        var hasMeters = (await client.From<UtilityMeterEntity>()
            .Filter("unit_id", Operator.Equals, unitId.ToString())
            .Get()).Models.Count > 0;
        var hasWorkOrders = (await client.From<WorkOrderEntity>()
            .Filter("unit_id", Operator.Equals, unitId.ToString())
            .Get()).Models.Count > 0;
        if (hasTenancies || hasInvites || hasMeters || hasWorkOrders)
        {
            throw new InvalidOperationException(
                "This unit has tenancy, invite, utility meter, or work-order history and cannot be deleted.");
        }

        await client.From<UnitEntity>()
            .Filter("id", Operator.Equals, unitId.ToString())
            .Filter("landlord_id", Operator.Equals, landlordId.ToString())
            .Delete(new Supabase.Postgrest.QueryOptions
            {
                Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal
            });

        var remaining = (await client.From<UnitEntity>()
            .Filter("id", Operator.Equals, unitId.ToString())
            .Filter("landlord_id", Operator.Equals, landlordId.ToString())
            .Get()).Models.FirstOrDefault();
        if (remaining is not null)
            throw new InvalidOperationException("Supabase did not delete the unit. Check the units DELETE policy.");
    }

    public async Task<UnitEntity> UpdateUnitAsync(UnitEntity unit)
    {
        var client = await GetClientAsync();
        var res = await client.From<UnitEntity>().Update(unit);
        return res.Models.FirstOrDefault() ?? unit;
    }

    // ==========================================
    // 4 & 5. Tenancies & Co-Tenants
    // ==========================================
    public async Task<TenancyEntity?> GetActiveTenancyForTenantAsync(Guid tenantId)
    {
        var client = await GetClientAsync();
        // Check primary tenant
        var res = await client.From<TenancyEntity>()
            .Filter("primary_tenant_id", Operator.Equals, tenantId.ToString())
            .Filter("is_active", Operator.Equals, "true")
            .Get();

        if (res.Models.Count > 0) return res.Models.FirstOrDefault();

        // Check co-tenants
        var coRes = await client.From<TenancyCoTenantEntity>()
            .Filter("tenant_id", Operator.Equals, tenantId.ToString())
            .Get();

        foreach (var co in coRes.Models)
        {
            var tenancy = await GetTenancyByIdAsync(co.TenancyId);
            if (tenancy?.IsActive == true)
                return tenancy;
        }

        return null;
    }

    public async Task<IReadOnlyList<TenancyEntity>> GetTenanciesAsync(Guid? landlordId = null)
    {
        var client = await GetClientAsync();
        var res = await client.From<TenancyEntity>().Get();
        if (landlordId.HasValue && landlordId.Value != Guid.Empty)
        {
            var units = await GetUnitsAsync(landlordId);
            var unitIds = units.Select(u => u.Id).ToHashSet();
            return res.Models.Where(t => unitIds.Contains(t.UnitId)).ToList();
        }
        return res.Models;
    }

    public async Task<TenancyEntity?> GetTenancyByIdAsync(Guid id)
    {
        var client = await GetClientAsync();
        var res = await client.From<TenancyEntity>()
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<TenancyEntity> CreateTenancyAsync(TenancyEntity tenancy, IEnumerable<TenancyCoTenantEntity>? coTenants = null)
    {
        if (tenancy.Id == Guid.Empty) tenancy.Id = Guid.NewGuid();
        var client = await GetClientAsync();
        var res = await client.From<TenancyEntity>().Insert(tenancy);
        var created = res.Models.FirstOrDefault() ?? tenancy;

        if (coTenants != null)
        {
            foreach (var co in coTenants)
            {
                co.TenancyId = created.Id;
                try
                {
                    await client.From<TenancyCoTenantEntity>().Insert(co);
                }
                catch { }
            }
        }

        return created;
    }

    public async Task<IReadOnlyList<TenancyCoTenantEntity>> GetCoTenantsAsync(Guid tenancyId)
    {
        var client = await GetClientAsync();
        var res = await client.From<TenancyCoTenantEntity>()
            .Filter("tenancy_id", Operator.Equals, tenancyId.ToString())
            .Get();
        return res.Models;
    }

    public async Task RemoveTenantFromUnitAsync(Guid landlordId, Guid tenancyId, Guid tenantId)
    {
        var client = await GetClientAsync();
        EnsureLandlordSession(client, landlordId);

        var tenancy = await GetTenancyByIdAsync(tenancyId);
        if (tenancy is null)
            throw new InvalidOperationException("The tenancy could not be found for this landlord.");

        var unit = (await client.From<UnitEntity>()
            .Filter("id", Operator.Equals, tenancy.UnitId.ToString())
            .Filter("landlord_id", Operator.Equals, landlordId.ToString())
            .Get()).Models.FirstOrDefault();
        if (unit is null)
            throw new InvalidOperationException("The tenancy does not belong to this landlord.");

        if (tenancy.PrimaryTenantId == tenantId)
        {
            var endDate = DateTime.UtcNow.Date < tenancy.StartDate.Date
                ? tenancy.StartDate.Date
                : DateTime.UtcNow.Date;
            await client.From<TenancyEntity>()
                .Filter("id", Operator.Equals, tenancyId.ToString())
                .Set(row => row.IsActive, false)
                .Set(row => row.EndDate, endDate)
                .Update(new Supabase.Postgrest.QueryOptions
                {
                    Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal
                });

            var updated = await GetTenancyByIdAsync(tenancyId);
            if (updated is null || updated.IsActive)
                throw new InvalidOperationException("Supabase did not end the tenancy. Check the tenancies UPDATE policy.");
            return;
        }

        var coTenant = (await client.From<TenancyCoTenantEntity>()
            .Filter("tenancy_id", Operator.Equals, tenancyId.ToString())
            .Filter("tenant_id", Operator.Equals, tenantId.ToString())
            .Get()).Models.FirstOrDefault();
        if (coTenant is null)
            throw new InvalidOperationException("The tenant is not assigned to this tenancy.");

        await client.From<TenancyCoTenantEntity>()
            .Filter("tenancy_id", Operator.Equals, tenancyId.ToString())
            .Filter("tenant_id", Operator.Equals, tenantId.ToString())
            .Delete(new Supabase.Postgrest.QueryOptions
            {
                Returning = Supabase.Postgrest.QueryOptions.ReturnType.Minimal
            });

        var remainingCoTenant = (await client.From<TenancyCoTenantEntity>()
            .Filter("tenancy_id", Operator.Equals, tenancyId.ToString())
            .Filter("tenant_id", Operator.Equals, tenantId.ToString())
            .Get()).Models.FirstOrDefault();
        if (remainingCoTenant is not null)
            throw new InvalidOperationException("Supabase did not remove the tenant. Check the tenancy co-tenant DELETE policy.");
    }

    private static void EnsureLandlordSession(Supabase.Client client, Guid landlordId)
    {
        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentUserId) ||
            currentUserId != landlordId)
        {
            throw new InvalidOperationException("The requested property does not belong to the signed-in landlord.");
        }
    }

    // ==========================================
    // 6. Payments (public.payments)
    // ==========================================
    public async Task<IReadOnlyList<PaymentEntity>> GetPaymentsForTenancyAsync(Guid tenancyId)
    {
        var client = await GetClientAsync();
        var res = await client.From<PaymentEntity>()
            .Filter("tenancy_id", Operator.Equals, tenancyId.ToString())
            .Get();
        return res.Models.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<PaymentEntity>> GetPaymentsForTenantAsync(Guid tenantId)
    {
        var client = await GetClientAsync();
        var res = await client.From<PaymentEntity>()
            .Filter("payer_id", Operator.Equals, tenantId.ToString())
            .Get();
        return res.Models.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<PaymentEntity>> GetAllPaymentsAsync(Guid? landlordId = null)
    {
        var client = await GetClientAsync();
        if (landlordId is not { } ownerId || ownerId == Guid.Empty)
        {
            var allPayments = await client.From<PaymentEntity>().Get();
            return allPayments.Models.OrderByDescending(payment => payment.CreatedAt).ToList();
        }

        EnsureLandlordSession(client, ownerId);
        var tenancies = await GetTenanciesAsync(ownerId);
        var payments = new List<PaymentEntity>();
        foreach (var tenancy in tenancies)
        {
            payments.AddRange(await GetPaymentsForTenancyAsync(tenancy.Id));
        }

        return payments
            .DistinctBy(payment => payment.Id)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToList();
    }

    public async Task<PaymentEntity> RecordPaymentAsync(PaymentEntity payment)
    {
        if (payment.Amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(payment), "Payment amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(payment.Description))
            throw new ArgumentException("Payment description is required.", nameof(payment));
        if (payment.Method is not ("MANUAL_EFT" or "INSTANT_EFT" or "DEBICHECK" or "CARD"))
            throw new ArgumentException("Select a payment method supported by the database.", nameof(payment));

        var client = await GetClientAsync();
        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentUserId) ||
            currentUserId != payment.PayerId)
        {
            throw new InvalidOperationException("The payment payer must match the signed-in tenant.");
        }

        var activeTenancy = await GetActiveTenancyForTenantAsync(payment.PayerId);
        if (activeTenancy?.Id != payment.TenancyId)
            throw new InvalidOperationException("The payment must belong to the tenant's active tenancy.");

        if (payment.Id == Guid.Empty) payment.Id = Guid.NewGuid();
        payment.PaidAt ??= DateTime.UtcNow;
        payment.Status = "SETTLED";
        payment.GatewayReference = null;
        var res = await client.From<PaymentEntity>().Insert(payment);
        return res.Models.FirstOrDefault() ?? payment;
    }

    // ==========================================
    // 7 & 8. Utility Meters & Tokens
    // ==========================================
    public async Task<IReadOnlyList<UtilityMeterEntity>> GetUtilityMetersForUnitAsync(Guid unitId)
    {
        var client = await GetClientAsync();
        var res = await client.From<UtilityMeterEntity>()
            .Filter("unit_id", Operator.Equals, unitId.ToString())
            .Get();
        return res.Models;
    }

    public async Task<IReadOnlyList<UtilityTokenEntity>> GetUtilityTokensForMeterAsync(Guid meterId)
    {
        var client = await GetClientAsync();
        var res = await client.From<UtilityTokenEntity>()
            .Filter("meter_id", Operator.Equals, meterId.ToString())
            .Get();
        return res.Models.OrderByDescending(t => t.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<UtilityTokenEntity>> GetUtilityTokensForTenantAsync(Guid tenantId)
    {
        var tenancy = await GetActiveTenancyForTenantAsync(tenantId);
        if (tenancy == null) return Array.Empty<UtilityTokenEntity>();

        var meters = await GetUtilityMetersForUnitAsync(tenancy.UnitId);
        var tokens = new List<UtilityTokenEntity>();
        foreach (var meter in meters)
        {
            var meterTokens = await GetUtilityTokensForMeterAsync(meter.Id);
            tokens.AddRange(meterTokens);
        }
        return tokens.OrderByDescending(t => t.CreatedAt).ToList();
    }

    public async Task<UtilityTokenEntity> PurchaseUtilityTokenAsync(Guid meterId, Guid payerId, decimal amount, decimal unitsPurchased)
    {
        var client = await GetClientAsync();
        var meters = await client.From<UtilityMeterEntity>().Filter("id", Operator.Equals, meterId.ToString()).Get();
        var meter = meters.Models.FirstOrDefault();

        var tenancyId = Guid.Empty;
        if (meter != null)
        {
            var tenancies = await client.From<TenancyEntity>().Filter("unit_id", Operator.Equals, meter.UnitId.ToString()).Get();
            tenancyId = tenancies.Models.FirstOrDefault()?.Id ?? Guid.Empty;
        }

        // 1. Record payment
        var payment = new PaymentEntity
        {
            Id = Guid.NewGuid(),
            TenancyId = tenancyId,
            PayerId = payerId,
            Amount = amount,
            Description = $"Prepaid {(meter?.MeterType ?? "Utility")} Token Purchase",
            Method = "CARD",
            Status = "SETTLED",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        await client.From<PaymentEntity>().Insert(payment);

        // 2. Generate 16-digit token code
        var tokenCode = $"{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}";
        var token = new UtilityTokenEntity
        {
            Id = Guid.NewGuid(),
            MeterId = meterId,
            PaymentId = payment.Id,
            TokenCode = tokenCode,
            UnitsPurchased = unitsPurchased,
            CreatedAt = DateTime.UtcNow
        };
        var res = await client.From<UtilityTokenEntity>().Insert(token);
        return res.Models.FirstOrDefault() ?? token;
    }

    // ==========================================
    // 9. Work Orders (public.work_orders)
    // ==========================================
    public async Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForTenantAsync(Guid tenantId)
    {
        var client = await GetClientAsync();
        var res = await client.From<WorkOrderEntity>()
            .Filter("logged_by_id", Operator.Equals, tenantId.ToString())
            .Get();
        return res.Models.OrderByDescending(w => w.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForLandlordAsync(Guid landlordId)
    {
        var buildings = await GetBuildingsAsync(landlordId);
        if (buildings.Count == 0) return Array.Empty<WorkOrderEntity>();

        var client = await GetClientAsync();
        var workOrders = new List<WorkOrderEntity>();
        foreach (var building in buildings)
        {
            var res = await client.From<WorkOrderEntity>()
                .Filter("building_id", Operator.Equals, building.Id.ToString())
                .Get();
            workOrders.AddRange(res.Models);
        }

        // Supabase RLS further restricts these rows to work orders reported by
        // tenants associated with each building's units.
        return workOrders
            .DistinctBy(workOrder => workOrder.Id)
            .OrderByDescending(workOrder => workOrder.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForContractorAsync(Guid contractorId)
    {
        var client = await GetClientAsync();
        var res = await client.From<WorkOrderEntity>()
            .Filter("assigned_contractor_id", Operator.Equals, contractorId.ToString())
            .Get();
        return res.Models.OrderByDescending(w => w.CreatedAt).ToList();
    }

    public async Task<WorkOrderEntity?> GetWorkOrderByIdAsync(Guid id)
    {
        var client = await GetClientAsync();
        var res = await client.From<WorkOrderEntity>()
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<WorkOrderEntity?> GetContractorWorkOrderByDispatchPinAsync(Guid id, string pin)
    {
        var client = await GetClientAsync();
        return await client.Rpc<WorkOrderEntity>(
            "get_contractor_work_order_by_dispatch_pin",
            new { p_work_order_id = id, p_dispatch_pin = pin.Trim() });
    }

    public async Task<WorkOrderEntity> CreateWorkOrderAsync(WorkOrderEntity workOrder)
    {
        if (workOrder.Id == Guid.Empty) workOrder.Id = Guid.NewGuid();
        var unit = await GetUnitByIdAsync(workOrder.UnitId)
            ?? throw new InvalidOperationException("The work order unit could not be found.");
        workOrder.BuildingId = unit.BuildingId;
        workOrder.Status = "LOGGED";
        var client = await GetClientAsync();
        var res = await client.From<WorkOrderEntity>().Insert(workOrder);
        return res.Models.FirstOrDefault() ?? workOrder;
    }

    public async Task<WorkOrderEntity> UpdateWorkOrderStatusAsync(Guid workOrderId, string newStatus)
    {
        var client = await GetClientAsync();
        var res = await client.From<WorkOrderEntity>()
            .Filter("id", Operator.Equals, workOrderId.ToString())
            .Set(x => x.Status, newStatus)
            .Update();
        return res.Models.FirstOrDefault() ?? (await GetWorkOrderByIdAsync(workOrderId))!;
    }

    public async Task<WorkOrderEntity> AssignContractorAsync(Guid workOrderId, Guid contractorId)
    {
        var client = await GetClientAsync();
        var res = await client.From<WorkOrderEntity>()
            .Filter("id", Operator.Equals, workOrderId.ToString())
            .Set(x => x.AssignedContractorId, (Guid?)contractorId)
            .Set(x => x.Status, "CONTRACTOR_ASSIGNED")
            .Update();
        return res.Models.FirstOrDefault() ?? (await GetWorkOrderByIdAsync(workOrderId))!;
    }

    // ==========================================
    // 10. Contractor Quotes (public.contractor_quotes)
    // ==========================================
    public async Task<IReadOnlyList<ContractorQuoteEntity>> GetQuotesForWorkOrderAsync(Guid workOrderId)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorQuoteEntity>()
            .Filter("work_order_id", Operator.Equals, workOrderId.ToString())
            .Get();
        return res.Models.OrderByDescending(q => q.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ContractorQuoteEntity>> GetQuotesForContractorAsync(Guid contractorId)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorQuoteEntity>()
            .Filter("contractor_id", Operator.Equals, contractorId.ToString())
            .Get();
        return res.Models.OrderByDescending(q => q.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ContractorQuoteEntity>> GetAllQuotesAsync()
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorQuoteEntity>().Get();
        return res.Models.OrderByDescending(q => q.CreatedAt).ToList();
    }

    public async Task<ContractorQuoteEntity> SubmitQuoteAsync(ContractorQuoteEntity quote)
    {
        if (quote.Id == Guid.Empty) quote.Id = Guid.NewGuid();
        quote.TotalAmount = quote.LaborCost + quote.MaterialCost;
        quote.Status = "SUBMITTED";
        var client = await GetClientAsync();
        var res = await client.From<ContractorQuoteEntity>().Insert(quote);
        return res.Models.FirstOrDefault() ?? quote;
    }

    public async Task<ContractorQuoteEntity> UpdateQuoteStatusAsync(Guid quoteId, string status)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorQuoteEntity>()
            .Filter("id", Operator.Equals, quoteId.ToString())
            .Set(x => x.Status, status)
            .Update();
        return res.Models.FirstOrDefault() ?? new ContractorQuoteEntity { Id = quoteId, Status = status };
    }

    // ==========================================
    // 11. Job Proof Media (public.job_proof_media)
    // ==========================================
    public async Task<IReadOnlyList<JobProofMediaEntity>> GetProofMediaForWorkOrderAsync(Guid workOrderId)
    {
        var client = await GetClientAsync();
        var res = await client.From<JobProofMediaEntity>()
            .Filter("work_order_id", Operator.Equals, workOrderId.ToString())
            .Get();
        return res.Models.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<JobProofMediaEntity> UploadProofMediaAsync(JobProofMediaEntity media)
    {
        if (media.Id == Guid.Empty) media.Id = Guid.NewGuid();
        var client = await GetClientAsync();
        var res = await client.From<JobProofMediaEntity>().Insert(media);
        return res.Models.FirstOrDefault() ?? media;
    }

    // ==========================================
    // 12. Contractor Invoices (public.contractor_invoices)
    // ==========================================
    public async Task<IReadOnlyList<ContractorInvoiceEntity>> GetInvoicesForWorkOrderAsync(Guid workOrderId)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorInvoiceEntity>()
            .Filter("work_order_id", Operator.Equals, workOrderId.ToString())
            .Get();
        return res.Models.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ContractorInvoiceEntity>> GetInvoicesForContractorAsync(Guid contractorId)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorInvoiceEntity>()
            .Filter("contractor_id", Operator.Equals, contractorId.ToString())
            .Get();
        return res.Models.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ContractorInvoiceEntity>> GetAllInvoicesAsync(Guid? landlordId = null)
    {
        var client = await GetClientAsync();
        var res = await client.From<ContractorInvoiceEntity>().Get();
        return res.Models.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<ContractorInvoiceEntity> SubmitInvoiceAsync(ContractorInvoiceEntity invoice)
    {
        if (invoice.Id == Guid.Empty) invoice.Id = Guid.NewGuid();
        invoice.IsApprovedByLandlord = false;
        invoice.PayoutStatus = "PENDING";
        var client = await GetClientAsync();
        var res = await client.From<ContractorInvoiceEntity>().Insert(invoice);
        return res.Models.FirstOrDefault() ?? invoice;
    }

    public async Task<ContractorInvoiceEntity> ApproveInvoiceAsync(Guid invoiceId, bool isApproved)
    {
        var client = await GetClientAsync();
        var payout = isApproved ? "SETTLED" : "FAILED";
        var res = await client.From<ContractorInvoiceEntity>()
            .Filter("id", Operator.Equals, invoiceId.ToString())
            .Set(x => x.IsApprovedByLandlord, isApproved)
            .Set(x => x.PayoutStatus, payout)
            .Update();
        return res.Models.FirstOrDefault() ?? new ContractorInvoiceEntity { Id = invoiceId, IsApprovedByLandlord = isApproved, PayoutStatus = payout };
    }

    // ==========================================
    // 13. Inspections (public.inspections)
    // ==========================================
    public async Task<IReadOnlyList<InspectionEntity>> GetInspectionsForTenancyAsync(Guid tenancyId)
    {
        var client = await GetClientAsync();
        var res = await client.From<InspectionEntity>()
            .Filter("tenancy_id", Operator.Equals, tenancyId.ToString())
            .Get();
        return res.Models.OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<InspectionEntity?> GetInspectionByIdAsync(Guid id)
    {
        var client = await GetClientAsync();
        var res = await client.From<InspectionEntity>()
            .Filter("id", Operator.Equals, id.ToString())
            .Get();
        return res.Models.FirstOrDefault();
    }

    public async Task<InspectionEntity> CreateInspectionAsync(InspectionEntity inspection)
    {
        if (inspection.Id == Guid.Empty) inspection.Id = Guid.NewGuid();
        var client = await GetClientAsync();
        var res = await client.From<InspectionEntity>().Insert(inspection);
        return res.Models.FirstOrDefault() ?? inspection;
    }

    public async Task<InspectionEntity> SignInspectionAsync(Guid inspectionId, bool isTenant)
    {
        var client = await GetClientAsync();
        var query = client.From<InspectionEntity>().Filter("id", Operator.Equals, inspectionId.ToString());
        if (isTenant) query = query.Set(x => x.TenantSignedAt, (DateTime?)DateTime.UtcNow);
        else query = query.Set(x => x.LandlordSignedAt, (DateTime?)DateTime.UtcNow);
        var res = await query.Update();
        return res.Models.FirstOrDefault() ?? (await GetInspectionByIdAsync(inspectionId))!;
    }

    // ==========================================
    // Tenant invites (public.tenancy_invites, with in-memory fallback)
    // ==========================================
    private static readonly ConcurrentDictionary<Guid, TenancyInviteEntity> InviteFallback = new();
    private static int _inviteTableState; // 0 unknown, 1 available, -1 missing

    public async Task<TenancyInviteEntity> InviteTenantToUnitAsync(Guid landlordId, Guid unitId, string tenantEmail, DateTime startDate, DateTime endDate)
    {
        var email = NormalizeEmail(tenantEmail);
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("A tenant email address is required.");
        if (endDate.Date <= startDate.Date)
            throw new InvalidOperationException("Lease end date must be after the start date.");

        var unit = await GetUnitByIdAsync(unitId) ?? throw new InvalidOperationException("The selected unit could not be found.");
        if (unit.LandlordId != landlordId)
            throw new InvalidOperationException("You can only invite tenants to your own units.");

        var existingUser = await GetUserByEmailAsync(email);
        if (existingUser != null && !string.Equals(existingUser.Role, "TENANT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("That email belongs to a non-tenant account.");

        var existingInvites = await GetInvitesForLandlordAsync(landlordId);
        if (existingInvites.Any(i => i.UnitId == unitId && i.Status == "PENDING" && NormalizeEmail(i.TenantEmail) == email))
            throw new InvalidOperationException("A pending invite already exists for this tenant and unit.");

        if (existingUser != null && await TenantAlreadyOnUnitAsync(existingUser.Id, unitId))
            throw new InvalidOperationException("That tenant is already assigned to this unit.");

        var invite = new TenancyInviteEntity
        {
            Id = Guid.NewGuid(),
            UnitId = unitId,
            LandlordId = landlordId,
            TenantEmail = email,
            TenantId = existingUser?.Id,
            Status = "PENDING",
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            CreatedAt = DateTime.UtcNow
        };

        return await PersistInviteAsync(invite, isInsert: true);
    }

    public async Task<IReadOnlyList<TenancyInviteEntity>> GetInvitesForLandlordAsync(Guid landlordId)
    {
        var all = await LoadInvitesAsync();
        return all.Where(i => i.LandlordId == landlordId).OrderByDescending(i => i.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<TenancyInviteEntity>> GetPendingInvitesForTenantAsync(Guid tenantId, string? email = null)
    {
        var all = await LoadInvitesAsync();
        var cleanEmail = NormalizeEmail(email);
        return all
            .Where(i => i.Status == "PENDING" && MatchesTenant(i, tenantId, cleanEmail))
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    public async Task<TenancyInviteEntity> AcceptInviteAsync(Guid inviteId, Guid tenantId)
        => await RespondToInviteAsync(inviteId, tenantId, accept: true);

    public async Task<TenancyInviteEntity> DismissInviteAsync(Guid inviteId, Guid tenantId)
        => await RespondToInviteAsync(inviteId, tenantId, accept: false);

    private async Task<TenancyInviteEntity> RespondToInviteAsync(Guid inviteId, Guid tenantId, bool accept)
    {
        var invite = (await LoadInvitesAsync()).FirstOrDefault(i => i.Id == inviteId)
            ?? throw new InvalidOperationException("Invite not found.");
        if (!string.Equals(invite.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This invite has already been answered.");

        var tenant = await GetUserByIdAsync(tenantId)
            ?? throw new InvalidOperationException("Your tenant profile could not be found.");
        if (!MatchesTenant(invite, tenantId, tenant.Email))
            throw new InvalidOperationException("This invite was sent to a different email address.");

        invite.TenantId = tenantId;
        invite.RespondedAt = DateTime.UtcNow;
        invite.Status = accept ? "ACCEPTED" : "DISMISSED";
        var saved = await PersistInviteAsync(invite, isInsert: false);

        if (accept)
            await AssignTenantFromInviteAsync(saved, tenantId);

        return saved;
    }

    private async Task AssignTenantFromInviteAsync(TenancyInviteEntity invite, Guid tenantId)
    {
        if (await TenantAlreadyOnUnitAsync(tenantId, invite.UnitId))
            return;

        var tenancies = await GetTenanciesAsync();
        var active = tenancies.FirstOrDefault(t => t.UnitId == invite.UnitId && t.IsActive);
        if (active == null)
        {
            await CreateTenancyAsync(new TenancyEntity
            {
                UnitId = invite.UnitId,
                PrimaryTenantId = tenantId,
                StartDate = invite.StartDate,
                EndDate = invite.EndDate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }, new[]
            {
                new TenancyCoTenantEntity
                {
                    TenantId = tenantId,
                    RentSharePercentage = 100.00m
                }
            });
            return;
        }

        var coTenants = await GetCoTenantsAsync(active.Id);
        if (coTenants.Any(c => c.TenantId == tenantId))
            return;

        try
        {
            var client = await GetClientAsync();
            await client.From<TenancyCoTenantEntity>().Insert(new TenancyCoTenantEntity
            {
                TenancyId = active.Id,
                TenantId = tenantId,
                RentSharePercentage = 0.00m
            });
        }
        catch
        {
            // Co-tenant insert is best-effort if RLS blocks the row.
        }
    }

    private async Task<bool> TenantAlreadyOnUnitAsync(Guid tenantId, Guid unitId)
    {
        var tenancy = await GetActiveTenancyForTenantAsync(tenantId);
        return tenancy != null && tenancy.IsActive && tenancy.UnitId == unitId;
    }

    private async Task<IReadOnlyList<TenancyInviteEntity>> LoadInvitesAsync()
    {
        if (_inviteTableState == -1)
            return InviteFallback.Values.Select(CloneInvite).ToList();

        try
        {
            var client = await GetClientAsync();
            var res = await client.From<TenancyInviteEntity>().Get();
            _inviteTableState = 1;
            foreach (var invite in res.Models)
                InviteFallback[invite.Id] = CloneInvite(invite);
            return res.Models;
        }
        catch
        {
            _inviteTableState = -1;
            return InviteFallback.Values.Select(CloneInvite).ToList();
        }
    }

    private async Task<TenancyInviteEntity> PersistInviteAsync(TenancyInviteEntity invite, bool isInsert)
    {
        InviteFallback[invite.Id] = CloneInvite(invite);
        if (_inviteTableState == -1)
            return CloneInvite(invite);

        try
        {
            var client = await GetClientAsync();
            if (isInsert)
            {
                var res = await client.From<TenancyInviteEntity>().Insert(invite);
                _inviteTableState = 1;
                return res.Models.FirstOrDefault() ?? invite;
            }

            var update = await client.From<TenancyInviteEntity>()
                .Filter("id", Operator.Equals, invite.Id.ToString())
                .Set(x => x.Status, invite.Status)
                .Set(x => x.TenantId, invite.TenantId)
                .Set(x => x.RespondedAt, invite.RespondedAt)
                .Update();
            _inviteTableState = 1;
            return update.Models.FirstOrDefault() ?? invite;
        }
        catch
        {
            _inviteTableState = -1;
            return CloneInvite(invite);
        }
    }

    private static bool MatchesTenant(TenancyInviteEntity invite, Guid tenantId, string? email)
    {
        if (invite.TenantId.HasValue && invite.TenantId.Value == tenantId)
            return true;
        var cleanEmail = NormalizeEmail(email);
        return !string.IsNullOrEmpty(cleanEmail) &&
               string.Equals(NormalizeEmail(invite.TenantEmail), cleanEmail, StringComparison.Ordinal);
    }

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static TenancyInviteEntity CloneInvite(TenancyInviteEntity invite) => new()
    {
        Id = invite.Id,
        UnitId = invite.UnitId,
        LandlordId = invite.LandlordId,
        TenantEmail = invite.TenantEmail,
        TenantId = invite.TenantId,
        Status = invite.Status,
        StartDate = invite.StartDate,
        EndDate = invite.EndDate,
        CreatedAt = invite.CreatedAt,
        RespondedAt = invite.RespondedAt
    };
}
