using System;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RentLog.Models.Entities;

[Table("users")]
public class UserEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("email")]
    public string Email { get; set; } = string.Empty;

    [Column("phone_number")]
    public string? PhoneNumber { get; set; }

    [Column("role")]
    public string Role { get; set; } = "TENANT";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("contractor_profiles")]
public class ContractorProfileEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("company_name")]
    public string CompanyName { get; set; } = string.Empty;

    [Column("trade")]
    public string Trade { get; set; } = "GENERAL";

    [Column("tax_vat_number")]
    public string? TaxVatNumber { get; set; }

    [Column("bank_account_details")]
    public string BankAccountDetails { get; set; } = "{}";

    [Column("is_verified")]
    public bool IsVerified { get; set; } = false;

    [Column("average_rating")]
    public decimal AverageRating { get; set; } = 0.00m;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("buildings")]
public class BuildingEntity : BaseModel
{
    [PrimaryKey("buildingid", false)]
    public int Id { get; set; }

    [Column("landlordid")]
    public Guid LandlordId { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("address")]
    public string Address { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("units")]
public class UnitEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("landlord_id")]
    public Guid LandlordId { get; set; }

    [Column("building_id")]
    public int BuildingId { get; set; }

    [Column("unit_number")]
    public string UnitNumber { get; set; } = string.Empty;

    [Column("building_name")]
    public string BuildingName { get; set; } = string.Empty;

    [Column("street_address")]
    public string StreetAddress { get; set; } = string.Empty;

    [Column("monthly_rent")]
    public decimal MonthlyRent { get; set; }

    [Column("deposit_amount")]
    public decimal DepositAmount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("tenancies")]
public class TenancyEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("unit_id")]
    public Guid UnitId { get; set; }

    [Column("primary_tenant_id")]
    public Guid PrimaryTenantId { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("tenancy_invites")]
public class TenancyInviteEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("unit_id")]
    public Guid UnitId { get; set; }

    [Column("landlord_id")]
    public Guid LandlordId { get; set; }

    [Column("tenant_email")]
    public string TenantEmail { get; set; } = string.Empty;

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("responded_at")]
    public DateTime? RespondedAt { get; set; }
}

[Table("tenancy_co_tenants")]
public class TenancyCoTenantEntity : BaseModel
{
    [PrimaryKey("tenancy_id", false)]
    [Column("tenancy_id")]
    public Guid TenancyId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("rent_share_percentage")]
    public decimal RentSharePercentage { get; set; } = 100.00m;
}

[Table("payments")]
public class PaymentEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenancy_id")]
    public Guid TenancyId { get; set; }

    [Column("payer_id")]
    public Guid PayerId { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("method")]
    public string Method { get; set; } = "MANUAL_EFT";

    [Column("status")]
    public string Status { get; set; } = "PENDING";

    [Column("gateway_reference")]
    public string? GatewayReference { get; set; }

    [Column("paid_at")]
    public DateTime? PaidAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("utility_meters")]
public class UtilityMeterEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("unit_id")]
    public Guid UnitId { get; set; }

    [Column("meter_number")]
    public string MeterNumber { get; set; } = string.Empty;

    [Column("meter_type")]
    public string MeterType { get; set; } = "ELECTRICITY";

    [Column("vendor_provider")]
    public string VendorProvider { get; set; } = string.Empty;
}

[Table("utility_tokens")]
public class UtilityTokenEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("meter_id")]
    public Guid MeterId { get; set; }

    [Column("payment_id")]
    public Guid PaymentId { get; set; }

    [Column("token_code")]
    public string TokenCode { get; set; } = string.Empty;

    [Column("units_purchased")]
    public decimal UnitsPurchased { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("work_orders")]
public class WorkOrderEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("unit_id")]
    public Guid UnitId { get; set; }

    [Column("building_id")]
    public int BuildingId { get; set; }

    [Column("logged_by_id")]
    public Guid LoggedById { get; set; }

    [Column("assigned_contractor_id")]
    public Guid? AssignedContractorId { get; set; }

    [Column("category")]
    public string Category { get; set; } = "GENERAL";

    [Column("priority")]
    public string Priority { get; set; } = "MEDIUM";

    [Column("status")]
    public string Status { get; set; } = "LOGGED";

    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Column("scheduled_access_time")]
    public DateTime? ScheduledAccessTime { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("contractor_quotes")]
public class ContractorQuoteEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("contractor_id")]
    public Guid ContractorId { get; set; }

    [Column("labor_cost")]
    public decimal LaborCost { get; set; }

    [Column("material_cost")]
    public decimal MaterialCost { get; set; }

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("estimated_hours")]
    public decimal? EstimatedHours { get; set; }

    [Column("notes")]
    public string? Notes { get; set; }

    [Column("status")]
    public string Status { get; set; } = "SUBMITTED";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("job_proof_media")]
public class JobProofMediaEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("uploaded_by_id")]
    public Guid UploadedById { get; set; }

    [Column("proof_type")]
    public string ProofType { get; set; } = "BEFORE_PHOTO";

    [Column("media_url")]
    public string MediaUrl { get; set; } = string.Empty;

    [Column("latitude")]
    public decimal? Latitude { get; set; }

    [Column("longitude")]
    public decimal? Longitude { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("contractor_invoices")]
public class ContractorInvoiceEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("work_order_id")]
    public Guid WorkOrderId { get; set; }

    [Column("contractor_id")]
    public Guid ContractorId { get; set; }

    [Column("invoice_number")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Column("total_amount")]
    public decimal TotalAmount { get; set; }

    [Column("is_approved_by_landlord")]
    public bool IsApprovedByLandlord { get; set; } = false;

    [Column("payout_status")]
    public string PayoutStatus { get; set; } = "PENDING";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("inspections")]
public class InspectionEntity : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenancy_id")]
    public Guid TenancyId { get; set; }

    [Column("inspection_type")]
    public string InspectionType { get; set; } = "MOVE_IN";

    [Column("checklist_data")]
    public string ChecklistData { get; set; } = "{}";

    [Column("tenant_signed_at")]
    public DateTime? TenantSignedAt { get; set; }

    [Column("landlord_signed_at")]
    public DateTime? LandlordSignedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
