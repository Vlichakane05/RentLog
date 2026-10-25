using System;
using System.Collections.Generic;
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
        var client = await _clientProvider.GetClientAsync();
        var session = await client.Auth.SignIn(cleanEmail, password);
        if (!Guid.TryParse(session?.User?.Id, out var authenticatedUserId))
            return null;

        var profile = await LoadProfileAsync(client, authenticatedUserId);
        if (profile != null)
            return _currentUser = profile;

        var userRow = await _propertyService.GetUserByEmailAsync(cleanEmail);
        if (userRow == null || userRow.Id != authenticatedUserId)
            return null;

        var role = ParseUserRole(userRow.Role);
        return _currentUser = new AuthUser
        {
            Id = authenticatedUserId.ToString(),
            Email = userRow.Email,
            Name = userRow.FullName,
            PhoneNumber = userRow.PhoneNumber,
            Role = role
        };
    }

    public async Task<AuthUser?> SignUpAsync(string fullName, string email, string? phoneNumber, string role, string password)
    {
        var cleanEmail = email.Trim();
        var cleanRole = role.Trim().ToUpperInvariant();
        var client = await _clientProvider.GetClientAsync();
        var session = await client.Auth.SignUp(cleanEmail, password, new Supabase.Gotrue.SignUpOptions
        {
            Data = new Dictionary<string, object>
            {
                ["full_name"] = fullName.Trim(),
                ["phone_number"] = phoneNumber?.Trim() ?? string.Empty,
                ["role"] = cleanRole.ToLowerInvariant()
            }
        });

        if (!Guid.TryParse(session?.User?.Id, out var userId))
            throw new InvalidOperationException("Supabase Auth did not return a valid account ID.");

        if (!Guid.TryParse(client.Auth.CurrentUser?.Id, out var currentUserId) ||
            currentUserId != userId ||
            string.IsNullOrWhiteSpace(client.Auth.CurrentSession?.AccessToken))
        {
            throw new InvalidOperationException(
                "Your account was created, but Supabase did not start a session. Confirm your email, then sign in.");
        }

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
