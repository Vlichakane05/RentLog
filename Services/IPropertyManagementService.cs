using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RentLog.Models;
using RentLog.Models.Entities;

namespace RentLog.Services;

public interface IPropertyManagementService
{
    // Users
    Task<UserEntity?> GetUserByIdAsync(Guid id);
    Task<UserEntity?> GetUserByEmailAsync(string email);
    Task<UserEntity> CreateUserAsync(UserEntity user);
    Task<IReadOnlyList<UserEntity>> GetUsersByRoleAsync(string role);

    // Units
    Task<IReadOnlyList<UnitEntity>> GetUnitsAsync(Guid? landlordId = null);
    Task<UnitEntity?> GetUnitByIdAsync(Guid id);
    Task<UnitEntity> CreateUnitAsync(UnitEntity unit);
    Task<UnitEntity> UpdateUnitAsync(UnitEntity unit);

    // Tenancies & Co-tenants
    Task<TenancyEntity?> GetActiveTenancyForTenantAsync(Guid tenantId);
    Task<IReadOnlyList<TenancyEntity>> GetTenanciesAsync(Guid? landlordId = null);
    Task<TenancyEntity?> GetTenancyByIdAsync(Guid id);
    Task<TenancyEntity> CreateTenancyAsync(TenancyEntity tenancy, IEnumerable<TenancyCoTenantEntity>? coTenants = null);
    Task<IReadOnlyList<TenancyCoTenantEntity>> GetCoTenantsAsync(Guid tenancyId);

    // Tenant invites
    Task<TenancyInviteEntity> InviteTenantToUnitAsync(Guid landlordId, Guid unitId, string tenantEmail, DateTime startDate, DateTime endDate);
    Task<IReadOnlyList<TenancyInviteEntity>> GetInvitesForLandlordAsync(Guid landlordId);
    Task<IReadOnlyList<TenancyInviteEntity>> GetPendingInvitesForTenantAsync(Guid tenantId, string? email = null);
    Task<TenancyInviteEntity> AcceptInviteAsync(Guid inviteId, Guid tenantId);
    Task<TenancyInviteEntity> DismissInviteAsync(Guid inviteId, Guid tenantId);

    // Payments
    Task<IReadOnlyList<PaymentEntity>> GetPaymentsForTenancyAsync(Guid tenancyId);
    Task<IReadOnlyList<PaymentEntity>> GetPaymentsForTenantAsync(Guid tenantId);
    Task<IReadOnlyList<PaymentEntity>> GetAllPaymentsAsync(Guid? landlordId = null);
    Task<PaymentEntity> RecordPaymentAsync(PaymentEntity payment);

    // Utilities (Meters & Tokens)
    Task<IReadOnlyList<UtilityMeterEntity>> GetUtilityMetersForUnitAsync(Guid unitId);
    Task<IReadOnlyList<UtilityTokenEntity>> GetUtilityTokensForMeterAsync(Guid meterId);
    Task<IReadOnlyList<UtilityTokenEntity>> GetUtilityTokensForTenantAsync(Guid tenantId);
    Task<UtilityTokenEntity> PurchaseUtilityTokenAsync(Guid meterId, Guid payerId, decimal amount, decimal unitsPurchased);

    // Work Orders
    Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForTenantAsync(Guid tenantId);
    Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForLandlordAsync(Guid landlordId);
    Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForContractorAsync(Guid contractorId);
    Task<WorkOrderEntity?> GetWorkOrderByIdAsync(Guid id);
    Task<WorkOrderEntity> CreateWorkOrderAsync(WorkOrderEntity workOrder);
    Task<WorkOrderEntity> UpdateWorkOrderStatusAsync(Guid workOrderId, string newStatus);
    Task<WorkOrderEntity> AssignContractorAsync(Guid workOrderId, Guid contractorId);

    // Contractor Profiles
    Task<IReadOnlyList<ContractorProfileEntity>> GetContractorProfilesAsync();
    Task<ContractorProfileEntity?> GetContractorProfileByIdAsync(Guid id);
    Task<ContractorProfileEntity?> GetContractorProfileByUserIdAsync(Guid userId);
    Task<ContractorProfileEntity?> VerifyContractorPinAsync(string pin);

    // Quotes
    Task<IReadOnlyList<ContractorQuoteEntity>> GetQuotesForWorkOrderAsync(Guid workOrderId);
    Task<IReadOnlyList<ContractorQuoteEntity>> GetQuotesForContractorAsync(Guid contractorId);
    Task<IReadOnlyList<ContractorQuoteEntity>> GetAllQuotesAsync();
    Task<ContractorQuoteEntity> SubmitQuoteAsync(ContractorQuoteEntity quote);
    Task<ContractorQuoteEntity> UpdateQuoteStatusAsync(Guid quoteId, string status);

    // Job Proof Media
    Task<IReadOnlyList<JobProofMediaEntity>> GetProofMediaForWorkOrderAsync(Guid workOrderId);
    Task<JobProofMediaEntity> UploadProofMediaAsync(JobProofMediaEntity media);

    // Invoices
    Task<IReadOnlyList<ContractorInvoiceEntity>> GetInvoicesForWorkOrderAsync(Guid workOrderId);
    Task<IReadOnlyList<ContractorInvoiceEntity>> GetInvoicesForContractorAsync(Guid contractorId);
    Task<IReadOnlyList<ContractorInvoiceEntity>> GetAllInvoicesAsync(Guid? landlordId = null);
    Task<ContractorInvoiceEntity> SubmitInvoiceAsync(ContractorInvoiceEntity invoice);
    Task<ContractorInvoiceEntity> ApproveInvoiceAsync(Guid invoiceId, bool isApproved);

    // Inspections
    Task<IReadOnlyList<InspectionEntity>> GetInspectionsForTenancyAsync(Guid tenancyId);
    Task<InspectionEntity?> GetInspectionByIdAsync(Guid id);
    Task<InspectionEntity> CreateInspectionAsync(InspectionEntity inspection);
    Task<InspectionEntity> SignInspectionAsync(Guid inspectionId, bool isTenant);
}
