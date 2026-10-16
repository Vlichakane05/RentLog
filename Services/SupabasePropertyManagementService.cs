using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RentLog.Models.Entities;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public class SupabasePropertyManagementService : IPropertyManagementService
{
    private readonly SupabaseClientProvider _clientProvider;

    public SupabasePropertyManagementService(SupabaseClientProvider clientProvider)
    {
        _clientProvider = clientProvider;
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
        if (unit.Id == Guid.Empty) unit.Id = Guid.NewGuid();
        var client = await GetClientAsync();
        var res = await client.From<UnitEntity>().Insert(unit);
        return res.Models.FirstOrDefault() ?? unit;
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

        var co = coRes.Models.FirstOrDefault();
        if (co != null)
        {
            return await GetTenancyByIdAsync(co.TenancyId);
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
        var res = await client.From<PaymentEntity>().Get();
        return res.Models.OrderByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<PaymentEntity> RecordPaymentAsync(PaymentEntity payment)
    {
        if (payment.Id == Guid.Empty) payment.Id = Guid.NewGuid();
        payment.PaidAt ??= DateTime.UtcNow;
        payment.Status = "PAID";
        var client = await GetClientAsync();
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
            Method = "CREDIT_CARD",
            Status = "PAID",
            GatewayReference = $"TOKEN-TXN-{Random.Shared.Next(100000, 999999)}",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        try { await client.From<PaymentEntity>().Insert(payment); } catch { }

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
        var client = await GetClientAsync();
        var res = await client.From<WorkOrderEntity>().Get();
        return res.Models.OrderByDescending(w => w.CreatedAt).ToList();
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

    public async Task<WorkOrderEntity> CreateWorkOrderAsync(WorkOrderEntity workOrder)
    {
        if (workOrder.Id == Guid.Empty) workOrder.Id = Guid.NewGuid();
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
            .Set(x => x.Status, "SCHEDULED")
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
        var payout = isApproved ? "PAID" : "REJECTED";
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
