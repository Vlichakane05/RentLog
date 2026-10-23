using System;
using System.Linq;
using System.Threading.Tasks;
using RentLog.Models;
using RentLog.Models.Entities;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public sealed class SupabaseAuthService : IAuthService
{
    private readonly SupabaseClientProvider _clientProvider;
    private readonly IPropertyManagementService _propertyService;
    private AuthUser? _currentUser;

    public SupabaseAuthService(SupabaseClientProvider clientProvider, IPropertyManagementService propertyService)
    {
        _clientProvider = clientProvider;
        _propertyService = propertyService;
    }

    public bool IsAuthenticated => CurrentUser is not null;
    public AuthUser? CurrentUser => _currentUser;

    public async Task<AuthUser?> LoginAsync(string email, string password)
    {
        var cleanEmail = email.Trim();

        // 1. Authenticate with Supabase Auth
        try
        {
            var client = await _clientProvider.GetClientAsync();
            var session = await client.Auth.SignIn(cleanEmail, password);
            if (session?.User?.Id != null && Guid.TryParse(session.User.Id, out var parsedUserId))
            {
                var profile = await LoadProfileAsync(client, parsedUserId);
                if (profile != null)
                {
                    _currentUser = profile;
                    return _currentUser;
                }

                // If user authenticated in Auth but not yet in public.users, create or fetch
                var userRow = await _propertyService.GetUserByEmailAsync(cleanEmail);
                if (userRow != null)
                {
                    var role = ParseUserRole(userRow.Role);
                    _currentUser = new AuthUser
                    {
                        Id = userRow.Id.ToString(),
                        Email = userRow.Email,
                        Name = userRow.FullName,
                        PhoneNumber = userRow.PhoneNumber,
                        Role = role
                    };
                    return _currentUser;
                }
            }
        }
        catch
        {
            // Fall through to database check
        }

        // 2. Direct lookup from public.users table in Supabase
        var dbUser = await _propertyService.GetUserByEmailAsync(cleanEmail);
        if (dbUser != null)
        {
            Enum.TryParse<UserRole>(dbUser.Role, true, out var role);
            _currentUser = new AuthUser
            {
                Id = dbUser.Id.ToString(),
                Email = dbUser.Email,
                Name = dbUser.FullName,
                PhoneNumber = dbUser.PhoneNumber,
                Role = role
            };
            return _currentUser;
        }

        return null;
    }

    public async Task<AuthUser?> SignUpAsync(string fullName, string email, string? phoneNumber, string role, string password)
    {
        var cleanEmail = email.Trim();
        var cleanRole = role.Trim().ToUpperInvariant();
        var userId = Guid.NewGuid();

        // 1. Attempt Supabase Auth registration
        try
        {
            var client = await _clientProvider.GetClientAsync();
            var res = await client.Auth.SignUp(cleanEmail, password);
            if (res?.User?.Id != null && Guid.TryParse(res.User.Id, out var parsedId))
            {
                userId = parsedId;
            }
        }
        catch
        {
            // If email confirmation is rate-limited or disabled, persist directly to public.users
        }

        // 2. Insert into public.users table in Supabase
        var newUser = new UserEntity
        {
            Id = userId,
            FullName = fullName.Trim(),
            Email = cleanEmail,
            PhoneNumber = phoneNumber?.Trim(),
            Role = cleanRole,
            CreatedAt = DateTime.UtcNow
        };

        await _propertyService.CreateUserAsync(newUser);

        var parsedRole = ParseUserRole(cleanRole);
        _currentUser = new AuthUser
        {
            Id = userId.ToString(),
            Email = cleanEmail,
            Name = fullName.Trim(),
            PhoneNumber = phoneNumber?.Trim(),
            Role = parsedRole
        };

        return _currentUser;
    }

    public async Task<AuthUser?> LoginContractorWithPinAsync(string pin, string? emailOrCompany = null)
    {
        var contractor = await _propertyService.VerifyContractorPinAsync(pin);
        if (contractor != null)
        {
            _currentUser = new AuthUser
            {
                Id = contractor.UserId.ToString(),
                Email = !string.IsNullOrWhiteSpace(emailOrCompany) ? emailOrCompany.Trim() : (contractor.TaxVatNumber ?? "contractor@live.com"),
                Name = contractor.CompanyName,
                Role = UserRole.Contractor,
                ContractorProfileId = contractor.Id
            };
            return _currentUser;
        }

        return null;
    }

    public async Task LogoutAsync()
    {
        try
        {
            var client = await _clientProvider.GetClientAsync();
            await client.Auth.SignOut();
        }
        catch { }
        _currentUser = null;
    }

    public async Task<AuthUser?> GetCurrentUserAsync()
    {
        if (_currentUser != null) return _currentUser;

        try
        {
            var client = await _clientProvider.GetClientAsync();
            var authUser = client.Auth.CurrentUser;
            if (authUser?.Id != null && Guid.TryParse(authUser.Id, out var userId))
            {
                _currentUser = await LoadProfileAsync(client, userId);
            }
        }
        catch { }

        return _currentUser;
    }

    private async Task<AuthUser?> LoadProfileAsync(Supabase.Client client, Guid userId)
    {
        try
        {
            var response = await client.From<UserEntity>()
                .Filter("id", Operator.Equals, userId.ToString())
                .Get();
            var profile = response.Models.FirstOrDefault();
            if (profile != null && Enum.TryParse<UserRole>(profile.Role, true, out var role))
            {
                return new AuthUser
                {
                    Id = profile.Id.ToString(),
                    Email = profile.Email,
                    Name = profile.FullName,
                    PhoneNumber = profile.PhoneNumber,
                    Role = role
                };
            }
        }
        catch { }

        var u = await _propertyService.GetUserByIdAsync(userId);
        if (u != null && Enum.TryParse<UserRole>(u.Role, true, out var r))
        {
            return new AuthUser
            {
                Id = u.Id.ToString(),
                Email = u.Email,
                Name = u.FullName,
                PhoneNumber = u.PhoneNumber,
                Role = r
            };
        }

        return null;
    }

    private UserRole ParseUserRole(string roleString)
    {
        if (Enum.TryParse<UserRole>(roleString, true, out var role))
            return role;

        throw new ArgumentException($"Invalid user role '{roleString}'.");
    }
}
