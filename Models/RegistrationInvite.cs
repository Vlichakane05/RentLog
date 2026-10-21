namespace RentLog.Models;

public class UnitInvitePreview
{
    public string Status { get; set; } = "invalid";
    public string BuildingName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
}

public class ContractorInvitePreview
{
    public string Status { get; set; } = "invalid";
    public string LandlordName { get; set; } = string.Empty;
}

public class RegistrationInviteLink
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}
