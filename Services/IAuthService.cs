using RentLog.Models;

namespace RentLog.Services;

public interface IAuthService
{
    Task<AuthUser?> LoginAsync(string email, string password);
    Task<AuthUser?> SignUpAsync(string fullName, string email, string? phoneNumber, string role, string password);
    Task<AuthUser?> LoginContractorWithPinAsync(string pin, string? emailOrCompany = null);
    Task LogoutAsync();
    Task<AuthUser?> GetCurrentUserAsync();
    bool IsAuthenticated { get; }
    AuthUser? CurrentUser { get; }
}
