using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RentLog.Models;

namespace RentLog.Services;

public class MockAuthService : IAuthService
{
    private readonly IPropertyManagementService _propertyService;
    private AuthUser? _currentUser;

    public MockAuthService(IPropertyManagementService propertyService)
    {
        _propertyService = propertyService;
    }

    private static readonly Dictionary<string, (string Password, AuthUser User)> Users = new(StringComparer.OrdinalIgnoreCase)
    {
        {
            "tenant@example.com",
            (
                "password123",
                new AuthUser
                {
                    Id = MockPropertyManagementService.DemoTenantId.ToString(),
                    Email = "tenant@example.com",
                    Name = "John Tenant",
                    PhoneNumber = "+27 83 987 6543",
                    Role = UserRole.Tenant
                }
            )
        },
        {
            "newtenant@example.com",
            (
                "password123",
                new AuthUser
                {
                    Id = MockPropertyManagementService.UnassignedTenantId.ToString(),
                    Email = "newtenant@example.com",
                    Name = "Amina Newtenant",
                    PhoneNumber = "+27 81 000 1111",
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
                    Id = MockPropertyManagementService.DemoLandlordId.ToString(),
                    Email = "landlord@example.com",
                    Name = "Jane Landlord (Owner)",
                    PhoneNumber = "+27 82 123 4567",
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
                    Id = MockPropertyManagementService.DemoContractorUserId.ToString(),
                    Email = "contractor@example.com",
                    Name = "Alex Fixer (Apex Plumb)",
                    PhoneNumber = "+27 84 555 1234",
                    Role = UserRole.Contractor,
                    ContractorProfileId = MockPropertyManagementService.DemoContractorProfileId
                }
            )
        }
    };

    public bool IsAuthenticated => _currentUser != null;
    public AuthUser? CurrentUser => _currentUser;

    public Task<AuthUser?> LoginAsync(string email, string password)
    {
        var cleanEmail = email.Trim();
        if (Users.TryGetValue(cleanEmail, out var entry) && entry.Password == password)
        {
            _currentUser = entry.User;
            return Task.FromResult<AuthUser?>(_currentUser);
        }

        return Task.FromResult<AuthUser?>(null);
    }

    public async Task<AuthUser?> SignUpAsync(string fullName, string email, string? phoneNumber, string role, string password)
    {
        var cleanEmail = email.Trim();
        var cleanRole = role.Trim().ToUpperInvariant();
        var userId = Guid.NewGuid();

        Enum.TryParse<UserRole>(cleanRole, true, out var parsedRole);
        var newUser = new AuthUser
        {
            Id = userId.ToString(),
            Email = cleanEmail,
            Name = fullName.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            Role = parsedRole
        };

        Users[cleanEmail] = (password, newUser);
        _currentUser = newUser;

        // Also add to propertyService
        await _propertyService.CreateUserAsync(new Models.Entities.UserEntity
        {
            Id = userId,
            FullName = fullName.Trim(),
            Email = cleanEmail,
            PhoneNumber = phoneNumber?.Trim(),
            Role = cleanRole,
            CreatedAt = DateTime.UtcNow
        });

        return _currentUser;
    }

    public async Task<AuthUser?> LoginContractorWithPinAsync(string pin, string? emailOrCompany = null)
    {
        var profile = await _propertyService.VerifyContractorPinAsync(pin);
        if (profile != null)
        {
            _currentUser = new AuthUser
            {
                Id = profile.UserId.ToString(),
                Email = !string.IsNullOrWhiteSpace(emailOrCompany) ? emailOrCompany.Trim() : "contractor@apexplumb.co.za",
                Name = profile.CompanyName,
                Role = UserRole.Contractor,
                ContractorProfileId = profile.Id
            };
            return _currentUser;
        }

        if (!string.IsNullOrWhiteSpace(pin) && pin.Trim().Length >= 4)
        {
            _currentUser = new AuthUser
            {
                Id = MockPropertyManagementService.DemoContractorUserId.ToString(),
                Email = !string.IsNullOrWhiteSpace(emailOrCompany) ? emailOrCompany.Trim() : "contractor@apexplumb.co.za",
                Name = "Apex Plumb & Pipe Specialists",
                Role = UserRole.Contractor,
                ContractorProfileId = MockPropertyManagementService.DemoContractorProfileId
            };
            return _currentUser;
        }

        return null;
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
