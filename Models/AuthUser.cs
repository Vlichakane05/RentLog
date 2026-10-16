namespace RentLog.Models;

public class AuthUser
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string? PhoneNumber { get; set; }
    public Guid? ContractorProfileId { get; set; }
    public Guid? AssignedWorkOrderId { get; set; }
}

