using System.Security.Cryptography;
using RentLog.Models;

namespace RentLog.Services;

public sealed class MockLandlordContractorManagementService : ILandlordContractorManagementService
{
    private readonly List<ContractorAccount> _contractors =
    [
        new()
        {
            Id = "demo-contractor-1",
            AuthUserId = "contractor-user-001",
            DisplayName = "Avery Plumbing",
            Trades = ["Plumbing", "Drainage", "General Maintenance"],
            ServiceArea = "Cape Town",
            AllUnits = true,
            Active = true,
            DateAdded = DateTime.UtcNow.AddMonths(-2)
        },
        new()
        {
            Id = "demo-contractor-2",
            DisplayName = "Bright Wire Electrical",
            Trades = ["Electrical"],
            ServiceArea = "Cape Town",
            AllUnits = true,
            Active = false,
            DateAdded = DateTime.UtcNow.AddMonths(-1)
        }
    ];

    private readonly Dictionary<string, List<RegistrationInviteLink>> _unitInvites = [];
    private readonly Dictionary<string, ContractorInvite> _contractorInvites = [];

    public Task<IReadOnlyList<ContractorAccount>> GetLandlordContractorsAsync(AuthUser landlord)
    {
        EnsureDemoLandlord(landlord);
        lock (_contractors)
        {
            return Task.FromResult<IReadOnlyList<ContractorAccount>>(_contractors.ToList());
        }
    }

    public Task SetLandlordContractorActiveAsync(AuthUser landlord, string contractorId, bool active)
    {
        EnsureDemoLandlord(landlord);
        lock (_contractors)
        {
            var contractor = _contractors.FirstOrDefault(item => item.Id == contractorId)
                ?? throw new InvalidOperationException("The contractor was not found.");
            contractor.Active = active;
        }
        return Task.CompletedTask;
    }

    public Task RemoveContractorAsync(AuthUser landlord, string contractorId)
    {
        EnsureDemoLandlord(landlord);
        lock (_contractors)
        {
            _contractors.RemoveAll(item => item.Id == contractorId);
        }
        return Task.CompletedTask;
    }

    public Task<RegistrationInviteLink> CreateContractorInviteAsync(AuthUser landlord, TimeSpan? validity = null)
    {
        EnsureDemoLandlord(landlord);
        var invite = new RegistrationInviteLink
        {
            Token = CreateToken(),
            ExpiresAt = DateTime.UtcNow.Add(validity ?? TimeSpan.FromDays(30))
        };
        lock (_contractorInvites)
        {
            _contractorInvites.Add(invite.Token, new ContractorInvite(invite));
        }
        return Task.FromResult(invite);
    }

    public Task<RegistrationInviteLink> CreateUnitInviteAsync(AuthUser landlord, string unitId, TimeSpan? validity = null)
    {
        EnsureDemoLandlord(landlord);
        var token = CreateToken();
        lock (_unitInvites)
        {
            if (!_unitInvites.TryGetValue(unitId, out var invites))
            {
                invites = [];
                _unitInvites.Add(unitId, invites);
            }

            var invite = new RegistrationInviteLink
            {
                Token = token,
                ExpiresAt = DateTime.UtcNow.Add(validity ?? TimeSpan.FromDays(7))
            };
            invites.Add(invite);
            return Task.FromResult(invite);
        }
    }

    public Task<IReadOnlyList<RegistrationInviteLink>> GetUnitInvitesAsync(AuthUser landlord, string unitId)
    {
        EnsureDemoLandlord(landlord);
        lock (_unitInvites)
        {
            IReadOnlyList<RegistrationInviteLink> invites = _unitInvites.TryGetValue(unitId, out var rows)
                ? rows.Where(invite => invite.ExpiresAt > DateTime.UtcNow).ToList()
                : [];
            return Task.FromResult(invites);
        }
    }

    public ContractorInvitePreview GetContractorInvitePreview(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new ContractorInvitePreview { Status = "invalid" };
        }

        lock (_contractorInvites)
        {
            if (!_contractorInvites.TryGetValue(token, out var invite))
            {
                return new ContractorInvitePreview { Status = "invalid" };
            }

            return new ContractorInvitePreview
            {
                Status = invite.Used
                    ? "used"
                    : invite.Link.ExpiresAt <= DateTime.UtcNow
                        ? "expired"
                        : "valid",
                LandlordName = "Jane Landlord"
            };
        }
    }

    public void RedeemContractorInvite(
        AuthUser contractor,
        string token,
        string displayName,
        IReadOnlyDictionary<string, string> contactDetails,
        string serviceArea,
        IReadOnlyList<string> trades)
    {
        EnsureContractor(contractor);
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 120 ||
            contactDetails is null || trades is null ||
            trades.Count == 0 || trades.Any(trade => string.IsNullOrWhiteSpace(trade) || trade.Trim().Length > 80) ||
            string.IsNullOrWhiteSpace(serviceArea) || serviceArea.Trim().Length > 200)
        {
            throw new ArgumentException("A name, contact details, service area, and at least one valid trade are required.");
        }

        lock (_contractorInvites)
        {
            var invite = GetAvailableInvite(token);
            lock (_contractors)
            {
                if (_contractors.Any(item => item.AuthUserId == contractor.Id))
                {
                    throw new InvalidOperationException("This account already has a contractor profile.");
                }

                _contractors.Add(new ContractorAccount
                {
                    Id = $"demo-contractor-{Guid.NewGuid():N}",
                    AuthUserId = contractor.Id,
                    DisplayName = displayName.Trim(),
                    ContactDetails = new Dictionary<string, string>(contactDetails),
                    ServiceArea = serviceArea.Trim(),
                    Trades = trades.Select(trade => trade.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    Active = true,
                    AllUnits = true,
                    DateAdded = DateTime.UtcNow
                });
            }

            invite.Used = true;
        }
    }

    public void LinkContractorInvite(AuthUser contractor, string token)
    {
        EnsureContractor(contractor);
        lock (_contractorInvites)
        {
            var invite = GetAvailableInvite(token);
            lock (_contractors)
            {
                if (!_contractors.Any(item => item.AuthUserId == contractor.Id))
                {
                    throw new InvalidOperationException("This account has no contractor profile.");
                }
            }

            invite.Used = true;
        }
    }

    private ContractorInvite GetAvailableInvite(string token)
    {
        if (string.IsNullOrWhiteSpace(token) ||
            !_contractorInvites.TryGetValue(token.Trim(), out var invite))
        {
            throw new InvalidOperationException("Contractor invite is invalid, expired, or already used.");
        }

        if (invite.Used || invite.Link.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Contractor invite is invalid, expired, or already used.");
        }

        return invite;
    }

    private static void EnsureContractor(AuthUser contractor)
    {
        ArgumentNullException.ThrowIfNull(contractor);
        if (contractor.Role != UserRole.Contractor)
        {
            throw new InvalidOperationException("Only a contractor account can use a contractor invitation.");
        }
    }

    private static void EnsureDemoLandlord(AuthUser landlord)
    {
        if (landlord.Role != UserRole.Landlord || landlord.Id != "landlord-001")
        {
            throw new InvalidOperationException("Only the demo landlord can manage contractors in demo mode.");
        }
    }

    private static string CreateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private sealed class ContractorInvite(RegistrationInviteLink link)
    {
        public RegistrationInviteLink Link { get; } = link;
        public bool Used { get; set; }
    }
}
