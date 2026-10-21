using RentLog.Models;

namespace RentLog.Services;

public class MockAuthService : IAuthService
{
    private readonly MockLandlordContractorManagementService _contractorManagement;
    private AuthUser? _currentUser;

    public MockAuthService(MockLandlordContractorManagementService contractorManagement)
    {
        _contractorManagement = contractorManagement;
    }

    // Demo credentials are intentionally simple; replace this mock before production.
    private static readonly object UsersLock = new();
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
        },
        {
            "contractor@example.com",
            (
                "password123",
                new AuthUser
                {
                    Id = "contractor-user-001",
                    Email = "contractor@example.com",
                    Name = "Avery Plumbing",
                    Role = UserRole.Contractor
                }
            )
        }
    };

    public bool IsAuthenticated => _currentUser != null;
    public AuthUser? CurrentUser => _currentUser;

    public Task<AuthUser?> LoginAsync(string email, string password)
    {
        lock (UsersLock)
        {
            if (Users.TryGetValue(email.Trim(), out var userEntry) && userEntry.Password == password)
            {
                _currentUser = userEntry.User;
                return Task.FromResult<AuthUser?>(_currentUser);
            }
        }

        return Task.FromResult<AuthUser?>(null);
    }

    public Task<AuthUser?> RegisterAsync(
        string email,
        string password,
        IReadOnlyDictionary<string, object> userData)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) ||
            userData is null ||
            !userData.TryGetValue("contractor_invite_token", out var tokenValue) ||
            tokenValue is not string token ||
            !userData.TryGetValue("full_name", out var nameValue) ||
            nameValue is not string name ||
            !userData.TryGetValue("contractor_trades", out var tradesValue) ||
            tradesValue is not string[] trades ||
            !userData.TryGetValue("service_area", out var serviceAreaValue) ||
            serviceAreaValue is not string serviceArea)
        {
            throw new InvalidOperationException("Demo registration requires a valid contractor invitation and complete contractor details.");
        }

        var normalizedEmail = email.Trim();
        lock (UsersLock)
        {
            if (Users.ContainsKey(normalizedEmail))
            {
                throw new InvalidOperationException("An account with this email address already exists. Use the existing account option.");
            }

            var user = new AuthUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = normalizedEmail,
                Name = name.Trim(),
                Role = UserRole.Contractor
            };
            _contractorManagement.RedeemContractorInvite(
                user,
                token,
                user.Name,
                new Dictionary<string, string> { ["email"] = normalizedEmail },
                serviceArea,
                trades);
            Users.Add(normalizedEmail, (password, user));
            _currentUser = user;
        }

        return Task.FromResult<AuthUser?>(_currentUser);
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
