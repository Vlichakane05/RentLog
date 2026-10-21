using RentLog.Models;
using static Supabase.Postgrest.Constants;

namespace RentLog.Services;

public sealed class SupabaseAuthService(SupabaseClientProvider clientProvider) : IAuthService
{
    public bool IsAuthenticated => CurrentUser is not null;
    public AuthUser? CurrentUser { get; private set; }

    public async Task<AuthUser?> LoginAsync(string email, string password)
    {
        var client = await clientProvider.GetClientAsync();
        await client.Auth.SignIn(email.Trim(), password);
        CurrentUser = await LoadProfileAsync(client);
        if (CurrentUser is null)
        {
            await client.Auth.SignOut();
            throw new InvalidOperationException("Authentication succeeded, but this account has no Rent Log profile. Ask the property administrator to finish account setup.");
        }

        return CurrentUser;
    }

    public async Task<AuthUser?> RegisterAsync(
        string email,
        string password,
        IReadOnlyDictionary<string, object> userData)
    {
        ArgumentNullException.ThrowIfNull(userData);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("An email address and password are required.");
        }

        var client = await clientProvider.GetClientAsync();
        await client.Auth.SignUp(
            email.Trim(),
            password,
            new Supabase.Gotrue.SignUpOptions
            {
                Data = userData.ToDictionary(entry => entry.Key, entry => entry.Value)
            });
        CurrentUser = await LoadProfileAsync(client);
        return CurrentUser;
    }

    public async Task LogoutAsync()
    {
        var client = await clientProvider.GetClientAsync();
        await client.Auth.SignOut();
        CurrentUser = null;
    }

    public async Task<AuthUser?> GetCurrentUserAsync()
    {
        if (CurrentUser is not null)
        {
            return CurrentUser;
        }

        var client = await clientProvider.GetClientAsync();
        if (client.Auth.CurrentUser is null)
        {
            return null;
        }

        CurrentUser = await LoadProfileAsync(client);
        return CurrentUser;
    }

    private static async Task<AuthUser?> LoadProfileAsync(Supabase.Client client)
    {
        var authUser = client.Auth.CurrentUser;
        if (authUser?.Id is null || !Guid.TryParse(authUser.Id, out var userId))
        {
            return null;
        }

        var response = await client.From<UserRow>()
            .Filter("id", Operator.Equals, userId.ToString())
            .Get();
        var profile = response.Models.FirstOrDefault();
        if (profile is null || !Enum.TryParse<UserRole>(profile.Role, true, out var role))
        {
            return null;
        }

        return new AuthUser
        {
            Id = profile.Id.ToString(),
            Email = profile.Email,
            Name = profile.FullName,
            Role = role
        };
    }
}
