using RentLog.Models;

namespace RentLog.Services;

public interface IAuthService
{
    Task<AuthUser?> LoginAsync(string email, string password);
    Task<AuthUser> RegisterAsync(string fullName, string email, string password, UserRole role) =>
        throw new NotSupportedException("Registration is not supported by the configured authentication provider.");
    Task RequestPasswordResetAsync(string email, string resetBaseUrl) =>
        throw new NotSupportedException("Password recovery is not supported by the configured authentication provider.");
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword) =>
        throw new NotSupportedException("Password recovery is not supported by the configured authentication provider.");
    Task LogoutAsync();
    Task<AuthUser?> GetCurrentUserAsync();
    bool IsAuthenticated { get; }
    AuthUser? CurrentUser { get; }
}
