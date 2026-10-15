using RentLog.Models;

namespace RentLog.Services;

public class MockAuthService : IAuthService
{
    private AuthUser? _currentUser;

    // Demo credentials are intentionally simple; replace this mock before production.
    private static readonly Dictionary<string, (string Password, AuthUser User)> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            "tenant@example.com",
            (
                "password123", // In production this would be hashed
                new AuthUser
                {
                    Id = "tenant-001",
                    Email = "tenant@example.com",
                    Name = "John Tenant",
                    Role = UserRole.Tenant
                }
            )
        },
        {
            "landlord@example.com",
            (
                "password123",
                new AuthUser
                {
                    Id = "landlord-001",
                    Email = "landlord@example.com",
                    Name = "Jane Landlord",
                    Role = UserRole.Landlord
                }
            )
        }
    };

    public bool IsAuthenticated => _currentUser != null;
    public AuthUser? CurrentUser => _currentUser;

    public Task<AuthUser?> LoginAsync(string email, string password)
    {
        if (Users.TryGetValue(email.Trim(), out var userEntry) && userEntry.Password == password)
        {
            _currentUser = userEntry.User;
            return Task.FromResult<AuthUser?>(_currentUser);
        }

        return Task.FromResult<AuthUser?>(null);
    }

    public Task LogoutAsync()
    {
        _currentUser = null;
        return Task.CompletedTask;
    }

    public Task<AuthUser?> GetCurrentUserAsync()
    {
        return Task.FromResult(_currentUser);
    }
}
