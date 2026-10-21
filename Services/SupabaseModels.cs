using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace RentLog.Services;

[Table("users")]
internal sealed class UserRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("email")]
    public string Email { get; set; } = string.Empty;
    [Column("full_name")]
    public string FullName { get; set; } = string.Empty;
    [Column("role")]
    public string Role { get; set; } = string.Empty;
}

[Table("preview_unit_invite")]
internal sealed class UnitInvitePreviewRow : BaseModel
{
    [Column("status")]
    public string Status { get; set; } = "invalid";
    [Column("building_name")]
    public string BuildingName { get; set; } = string.Empty;
    [Column("unit_name")]
    public string UnitName { get; set; } = string.Empty;
}

[Table("preview_contractor_invite")]
internal sealed class ContractorInvitePreviewRow : BaseModel
{
    [Column("status")]
    public string Status { get; set; } = "invalid";
    [Column("landlord_name")]
    public string LandlordName { get; set; } = string.Empty;
}

[Table("properties")]
internal sealed class PropertyRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("landlord_id")]
    public Guid LandlordId { get; set; }
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    [Column("address")]
    public string Address { get; set; } = string.Empty;
}

[Table("units")]
internal sealed class UnitRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("property_id")]
    public Guid PropertyId { get; set; }
    [Column("unit_number")]
    public string UnitNumber { get; set; } = string.Empty;
    [Column("monthly_rent")]
    public decimal MonthlyRent { get; set; }
}

[Table("tenancies")]
internal sealed class TenancyRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("unit_id")]
    public Guid UnitId { get; set; }
    [Column("tenant_id")]
    public Guid TenantId { get; set; }
    [Column("start_date")]
    public DateTime StartDate { get; set; }
    [Column("end_date")]
    public DateTime? EndDate { get; set; }
    [Column("is_active")]
    public bool IsActive { get; set; }
}

[Table("issues")]
internal sealed class IssueRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("property_id")]
    public Guid PropertyId { get; set; }
    [Column("unit_id")]
    public Guid UnitId { get; set; }
    [Column("tenant_id")]
    public Guid TenantId { get; set; }
    [Column("landlord_id")]
    public Guid LandlordId { get; set; }
    [Column("unit_number")]
    public string UnitNumber { get; set; } = string.Empty;
    [Column("title")]
    public string Title { get; set; } = string.Empty;
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    [Column("severity")]
    public string Severity { get; set; } = string.Empty;
    [Column("status")]
    public string Status { get; set; } = string.Empty;
    [Column("is_disputed")]
    public bool IsDisputed { get; set; }
    [Column("contractor_name")]
    public string? ContractorName { get; set; }
    [Column("category")]
    public string? Category { get; set; }
    [Column("preferred_windows")]
    public List<string> PreferredWindows { get; set; } = [];
    [Column("released_to_contractors")]
    public bool ReleasedToContractors { get; set; }
    [Column("assigned_contractor_id")]
    public Guid? AssignedContractorId { get; set; }
    [Column("contractor_status")]
    public string ContractorStatus { get; set; } = "Reported";
    [Column("accepted_at")]
    public DateTime? AcceptedAt { get; set; }
    [Column("landlord_seen_at")]
    public DateTime? LandlordSeenAt { get; set; }
    [Column("work_done_at")]
    public DateTime? WorkDoneAt { get; set; }
    [Column("tenant_verified_at")]
    public DateTime? TenantVerifiedAt { get; set; }
    [Column("reopen_note")]
    public string? ReopenNote { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("tenant_issue_contractor_details")]
internal sealed class TenantIssueContractorDetailsRow : BaseModel
{
    [Column("contractor_first_name")]
    public string ContractorFirstName { get; set; } = string.Empty;
    [Column("contractor_trade")]
    public string ContractorTrade { get; set; } = string.Empty;
}

[Table("issue_events")]
internal sealed class IssueEventRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("issue_id")]
    public Guid IssueId { get; set; }
    [Column("author_id")]
    public Guid AuthorId { get; set; }
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    [Column("author_name")]
    public string AuthorName { get; set; } = string.Empty;
    [Column("author_role")]
    public string AuthorRole { get; set; } = string.Empty;
    [Column("new_status")]
    public string? NewStatus { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("media")]
internal sealed class MediaRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("issue_id")]
    public Guid IssueId { get; set; }
    [Column("event_id")]
    public Guid? EventId { get; set; }
    [Column("uploaded_by")]
    public Guid UploadedBy { get; set; }
    [Column("storage_path")]
    public string StoragePath { get; set; } = string.Empty;
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;
    [Column("mime_type")]
    public string MimeType { get; set; } = string.Empty;
    [Column("file_size_bytes")]
    public long? FileSizeBytes { get; set; }
    [Column("alt_text")]
    public string AltText { get; set; } = string.Empty;
    [Column("duration_seconds")]
    public double? DurationSeconds { get; set; }
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("issue_quotes")]
internal sealed class IssueQuoteRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("issue_id")]
    public Guid IssueId { get; set; }
    [Column("contractor_name")]
    public string ContractorName { get; set; } = string.Empty;
    [Column("amount")]
    public decimal Amount { get; set; }
    [Column("work_description")]
    public string WorkDescription { get; set; } = string.Empty;
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;
    [Column("storage_path")]
    public string StoragePath { get; set; } = string.Empty;
    [Column("status")]
    public string Status { get; set; } = string.Empty;
    [Column("submitted_by")]
    public Guid SubmittedBy { get; set; }
    [Column("submitted_at")]
    public DateTime SubmittedAt { get; set; }
}

[Table("rent_payments")]
internal sealed class RentPaymentRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("tenancy_id")]
    public Guid TenancyId { get; set; }
    [Column("unit_id")]
    public Guid UnitId { get; set; }
    [Column("tenant_id")]
    public Guid TenantId { get; set; }
    [Column("period")]
    public DateTime Period { get; set; }
    [Column("amount")]
    public decimal Amount { get; set; }
    [Column("due_date")]
    public DateTime DueDate { get; set; }
    [Column("paid_date")]
    public DateTime? PaidDate { get; set; }
    [Column("status")]
    public string Status { get; set; } = string.Empty;
    [Column("last_reminder_at")]
    public DateTime? LastReminderAt { get; set; }
    [Column("reminder_count")]
    public int ReminderCount { get; set; }
}

[Table("contractors")]
internal sealed class ContractorRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("auth_user_id")]
    public Guid AuthUserId { get; set; }
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;
    [Column("contact_details")]
    public Dictionary<string, string> ContactDetails { get; set; } = [];
    [Column("service_area")]
    public string ServiceArea { get; set; } = string.Empty;
}

[Table("contractor_trades")]
internal sealed class ContractorTradeRow : BaseModel
{
    [PrimaryKey("contractor_id", false)]
    public Guid ContractorId { get; set; }
    [Column("category")]
    public string Category { get; set; } = string.Empty;
}

[Table("landlord_contractors")]
internal sealed class LandlordContractorRow : BaseModel
{
    [PrimaryKey("landlord_id", false)]
    public Guid LandlordId { get; set; }
    [Column("contractor_id")]
    public Guid ContractorId { get; set; }
    [Column("active")]
    public bool Active { get; set; }
    [Column("all_units")]
    public bool AllUnits { get; set; }
    [Column("date_added")]
    public DateTime DateAdded { get; set; }
}

[Table("contractor_unit_access")]
internal sealed class ContractorUnitAccessRow : BaseModel
{
    [PrimaryKey("contractor_id", false)]
    public Guid ContractorId { get; set; }
    [Column("landlord_id")]
    public Guid LandlordId { get; set; }
    [Column("unit_id")]
    public Guid UnitId { get; set; }
}

[Table("contractor_invites")]
internal sealed class ContractorInviteRow : BaseModel
{
    [PrimaryKey("token", false)]
    public string Token { get; set; } = string.Empty;
    [Column("landlord_id")]
    public Guid LandlordId { get; set; }
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }
    [Column("used")]
    public bool Used { get; set; }
}

[Table("unit_invites")]
internal sealed class UnitInviteRow : BaseModel
{
    [PrimaryKey("token", false)]
    public string Token { get; set; } = string.Empty;
    [Column("unit_id")]
    public Guid UnitId { get; set; }
    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }
    [Column("used")]
    public bool Used { get; set; }
}

[Table("contractor_job_feed")]
internal sealed class ContractorJobRow : BaseModel
{
    [PrimaryKey("issue_id", false)]
    public Guid IssueId { get; set; }
    [Column("title")]
    public string Title { get; set; } = string.Empty;
    [Column("description")]
    public string Description { get; set; } = string.Empty;
    [Column("landlord_id")]
    public Guid LandlordId { get; set; }
    [Column("landlord_label")]
    public string LandlordLabel { get; set; } = string.Empty;
    [Column("building_label")]
    public string BuildingLabel { get; set; } = string.Empty;
    [Column("suburb")]
    public string? Suburb { get; set; }
    [Column("category")]
    public string? Category { get; set; }
    [Column("preferred_windows")]
    public List<string> PreferredWindows { get; set; } = [];
    [Column("severity")]
    public string Severity { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("contractor_status")]
    public string ContractorStatus { get; set; } = string.Empty;
    [Column("accepted_at")]
    public DateTime? AcceptedAt { get; set; }
    [Column("landlord_seen_at")]
    public DateTime? LandlordSeenAt { get; set; }
    [Column("work_done_at")]
    public DateTime? WorkDoneAt { get; set; }
    [Column("tenant_verified_at")]
    public DateTime? TenantVerifiedAt { get; set; }
    [Column("reopen_note")]
    public string? ReopenNote { get; set; }
    [Column("assigned_contractor_id")]
    public Guid? AssignedContractorId { get; set; }
    [Column("property_address")]
    public string? PropertyAddress { get; set; }
    [Column("unit_number")]
    public string? UnitNumber { get; set; }
    [Column("assigned_to_me")]
    public bool AssignedToMe { get; set; }
}

[Table("contractor_media_feed")]
internal sealed class ContractorMediaRow : BaseModel
{
    [Column("issue_id")]
    public Guid IssueId { get; set; }
    [PrimaryKey("storage_path", false)]
    public string StoragePath { get; set; } = string.Empty;
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;
    [Column("mime_type")]
    public string MimeType { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

[Table("landlord_notifications")]
internal sealed class LandlordNotificationRow : BaseModel
{
    [PrimaryKey("id", false)]
    public Guid Id { get; set; }
    [Column("landlord_id")]
    public Guid LandlordId { get; set; }
    [Column("issue_id")]
    public Guid IssueId { get; set; }
    [Column("message")]
    public string Message { get; set; } = string.Empty;
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
    [Column("read_at")]
    public DateTime? ReadAt { get; set; }
}
