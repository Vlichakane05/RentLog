using RentLog.Models;

namespace RentLog.Services;

public interface IAuthService
{
    Task<AuthUser?> LoginAsync(string email, string password);
    Task LogoutAsync();
    Task<AuthUser?> GetCurrentUserAsync();
    bool IsAuthenticated { get; }
    AuthUser? CurrentUser { get; }
}
