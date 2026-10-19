using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentLog.Models;

namespace RentLog.Services.Sql;

public sealed class SqlAuthService(
    IDbContextFactory<SqlDb> factory,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<SqlAuthService> logger) : IAuthService
{
    private static readonly PasswordHasher<DbUser> Hasher = new();
    private static readonly ConcurrentDictionary<string, (int Count, DateTime LockedUntil)> Failures = new();
    private AuthUser? _current;

    public bool IsAuthenticated => _current is not null;
    public AuthUser? CurrentUser => _current;
    public Task<AuthUser?> GetCurrentUserAsync() => Task.FromResult(_current);
    public Task LogoutAsync() { _current = null; return Task.CompletedTask; }

    public async Task<AuthUser?> LoginAsync(string email, string password)
    {
        var key = NormalizeEmail(email);
        if (key.Length == 0 || string.IsNullOrEmpty(password)) return null;
        if (Failures.TryGetValue(key, out var f) && f.LockedUntil > DateTime.UtcNow) return null;

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == key);
        var ok = user is not null && Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
        if (!ok)
        {
            Failures.AddOrUpdate(key, _ => (1, DateTime.MinValue),
                (_, v) => v.Count >= 4 ? (0, DateTime.UtcNow.AddMinutes(5)) : (v.Count + 1, DateTime.MinValue));
            return null;
        }

        Failures.TryRemove(key, out _);
        _current = ToAuthUser(user!);
        return _current;
    }

    public async Task<AuthUser> RegisterAsync(string fullName, string email, string password, UserRole role)
    {
        var name = (fullName ?? "").Trim();
        var normalizedEmail = NormalizeEmail(email);
        if (name.Length is < 2 or > 120) throw new InvalidOperationException("Enter your full name (2–120 characters).");
        if (normalizedEmail.Length > 256 || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(normalizedEmail))
            throw new InvalidOperationException("Enter a valid email address.");
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            throw new InvalidOperationException("Your password must contain at least 8 characters.");
        if (!Enum.IsDefined(typeof(UserRole), role) || role is not (UserRole.Tenant or UserRole.Landlord))
            throw new InvalidOperationException("Choose Tenant or Landlord as your account type.");

        await using var db = await factory.CreateDbContextAsync();
        if (await db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw new InvalidOperationException("An account with that email address already exists. Try signing in instead.");

        var user = new DbUser { Email = normalizedEmail, FullName = name, Role = role };
        user.PasswordHash = Hasher.HashPassword(user, password);
        db.Users.Add(user);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Account registration failed due to a database constraint.");
            throw new InvalidOperationException("We couldn't create the account. If this email is already registered, sign in instead.");
        }
        return ToAuthUser(user);
    }

    public async Task RequestPasswordResetAsync(string email, string resetBaseUrl)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail.Length == 0 || normalizedEmail.Length > 256) return;

        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        // Deliberately return the same result for unknown accounts to avoid account enumeration.
        if (user is null) return;

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var tokenRecord = new DbPasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
        db.PasswordResetTokens.Add(tokenRecord);
        await db.SaveChangesAsync();

        var link = $"{resetBaseUrl.TrimEnd('/')}?email={Uri.EscapeDataString(normalizedEmail)}&token={Uri.EscapeDataString(rawToken)}";
        var smtp = configuration.GetSection("Smtp");
        var host = smtp["Host"];
        var from = smtp["From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            if (environment.IsDevelopment()) logger.LogWarning("Password reset link for {Email} (SMTP is not configured; development only): {ResetLink}", normalizedEmail, link);
            else logger.LogError("Password recovery email is not configured. Set Smtp:Host and Smtp:From.");
            return;
        }

        try
        {
            using var message = new MailMessage(from, normalizedEmail)
            {
                Subject = "Reset your Rent Log password",
                IsBodyHtml = true,
                Body = $"<p>Hello {WebUtility.HtmlEncode(user.FullName)},</p><p>Use the link below to reset your Rent Log password. It expires in 30 minutes and can only be used once.</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Reset my password</a></p><p>If you didn't request this, you can ignore this email.</p>"
            };
            using var client = new SmtpClient(host, int.TryParse(smtp["Port"], out var port) ? port : 587)
            {
                EnableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            var username = smtp["Username"];
            var password = smtp["Password"];
            if (!string.IsNullOrWhiteSpace(username)) client.Credentials = new NetworkCredential(username, password ?? "");
            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send a password recovery email to {Email}.", normalizedEmail);
            // Do not expose whether the account exists or disclose delivery internals to the browser.
        }
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8) return false;
        await using var db = await factory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail);
        if (user is null) return false;

        var tokenHash = HashToken(token);
        var record = await db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.TokenHash == tokenHash && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();
        if (record is null) return false;

        user.PasswordHash = Hasher.HashPassword(user, newPassword);
        record.UsedAt = DateTime.UtcNow;
        // Invalidate any other outstanding reset tokens for this user.
        var otherTokens = await db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.Id != record.Id && t.UsedAt == null)
            .ToListAsync();
        foreach (var other in otherTokens) other.UsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Failures.TryRemove(normalizedEmail, out _);
        return true;
    }

    private static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static AuthUser ToAuthUser(DbUser user) => new() { Id = user.Id.ToString(), Email = user.Email, Name = user.FullName, Role = user.Role };
}
