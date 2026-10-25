using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RentLog.Models;
using RentLog.Models.Entities;

namespace RentLog.Services;

public class MockPropertyManagementService : IPropertyManagementService
{
    // Seeded data stores for all 13 tables
    public static readonly Guid DemoLandlordId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid UnassignedTenantId = Guid.Parse("22222222-2222-2222-2222-222222222223");
    public static readonly Guid DemoInviteId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid DemoContractorUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid DemoContractorProfileId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly int DemoBuilding1Id = 1;
    public static readonly int DemoBuilding2Id = 2;
    public static readonly Guid DemoUnit1Id = Guid.Parse("55555555-5555-5555-5555-555555555551");
    public static readonly Guid DemoUnit2Id = Guid.Parse("55555555-5555-5555-5555-555555555552");
    public static readonly Guid DemoTenancyId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid DemoMeterId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid DemoWorkOrder1Id = Guid.Parse("88888888-8888-8888-8888-888888888881");
    public static readonly Guid DemoWorkOrder2Id = Guid.Parse("88888888-8888-8888-8888-888888888882");

    private readonly List<UserEntity> _users = new();
    private readonly List<ContractorProfileEntity> _contractors = new();
    private readonly List<BuildingEntity> _buildings = new();
    private readonly List<UnitEntity> _units = new();
    private readonly List<TenancyEntity> _tenancies = new();
    private readonly List<TenancyCoTenantEntity> _coTenants = new();
    private readonly List<TenancyInviteEntity> _invites = new();
    private readonly List<PaymentEntity> _payments = new();
    private readonly List<UtilityMeterEntity> _meters = new();
    private readonly List<UtilityTokenEntity> _tokens = new();
    private readonly List<WorkOrderEntity> _workOrders = new();
    private readonly List<ContractorQuoteEntity> _quotes = new();
    private readonly List<JobProofMediaEntity> _proofMedia = new();
    private readonly List<ContractorInvoiceEntity> _invoices = new();
    private readonly List<InspectionEntity> _inspections = new();

    private readonly object _lock = new();

    public MockPropertyManagementService()
    {
        SeedInitialData();
    }

    private void SeedInitialData()
    {
        // 1. Users
        _users.Add(new UserEntity
        {
            Id = DemoLandlordId,
            FullName = "Jane Landlord (Owner)",
            Email = "landlord@example.com",
            PhoneNumber = "+27 82 123 4567",
            Role = "LANDLORD",
            CreatedAt = DateTime.UtcNow.AddMonths(-12)
        });

        _users.Add(new UserEntity
        {
            Id = DemoTenantId,
            FullName = "John Tenant",
            Email = "tenant@example.com",
            PhoneNumber = "+27 83 987 6543",
            Role = "TENANT",
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });

        _users.Add(new UserEntity
        {
            Id = UnassignedTenantId,
            FullName = "Amina Newtenant",
            Email = "newtenant@example.com",
            PhoneNumber = "+27 81 000 1111",
            Role = "TENANT",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        _users.Add(new UserEntity
        {
            Id = DemoContractorUserId,
            FullName = "Alex Fixer",
            Email = "contractor@example.com",
            PhoneNumber = "+27 84 555 1234",
            Role = "CONTRACTOR",
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });

        // 2. Contractor Profiles
        _contractors.Add(new ContractorProfileEntity
        {
            Id = DemoContractorProfileId,
            UserId = DemoContractorUserId,
            CompanyName = "Apex Plumb & Pipe Specialists",
            Trade = "PLUMBING",
            TaxVatNumber = "ZA-492019482",
            BankAccountDetails = "{\"bank\":\"First National Bank\",\"account_number\":\"62819284918\",\"branch_code\":\"250655\"}",
            IsVerified = true,
            AverageRating = 4.9m,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });

        var electricContractorId = Guid.Parse("44444444-4444-4444-4444-444444444445");
        _contractors.Add(new ContractorProfileEntity
        {
            Id = electricContractorId,
            UserId = DemoContractorUserId,
            CompanyName = "VoltMaster Electrical Services",
            Trade = "ELECTRICAL",
            TaxVatNumber = "ZA-918237461",
            BankAccountDetails = "{\"bank\":\"Standard Bank\",\"account_number\":\"0129384719\",\"branch_code\":\"051001\"}",
            IsVerified = true,
            AverageRating = 4.8m,
            CreatedAt = DateTime.UtcNow.AddMonths(-4)
        });

        // 3. Buildings
        _buildings.Add(new BuildingEntity
        {
            Id = DemoBuilding1Id,
            LandlordId = DemoLandlordId,
            Name = "Atlantic Ocean View Residences",
            Address = "42 Beach Road, Sea Point, Cape Town",
            CreatedAt = DateTime.UtcNow.AddMonths(-10)
        });
        _buildings.Add(new BuildingEntity
        {
            Id = DemoBuilding2Id,
            LandlordId = DemoLandlordId,
            Name = "Sunset Ridge Estate",
            Address = "15 Kloof Nek Road, Tamboerskloof",
            CreatedAt = DateTime.UtcNow.AddMonths(-8)
        });

        // 4. Units
        _units.Add(new UnitEntity
        {
            Id = DemoUnit1Id,
            LandlordId = DemoLandlordId,
            BuildingId = DemoBuilding1Id,
            UnitNumber = "Unit 4B",
            BuildingName = "Atlantic Ocean View Residences",
            StreetAddress = "42 Beach Road, Sea Point, Cape Town",
            MonthlyRent = 14500.00m,
            DepositAmount = 29000.00m,
            CreatedAt = DateTime.UtcNow.AddMonths(-10)
        });

        _units.Add(new UnitEntity
        {
            Id = DemoUnit2Id,
            LandlordId = DemoLandlordId,
            BuildingId = DemoBuilding2Id,
            UnitNumber = "Apartment 12",
            BuildingName = "Sunset Ridge Estate",
            StreetAddress = "15 Kloof Nek Road, Tamboerskloof",
            MonthlyRent = 18000.00m,
            DepositAmount = 36000.00m,
            CreatedAt = DateTime.UtcNow.AddMonths(-8)
        });

        // 4. Tenancies
        _tenancies.Add(new TenancyEntity
        {
            Id = DemoTenancyId,
            UnitId = DemoUnit1Id,
            PrimaryTenantId = DemoTenantId,
            StartDate = DateTime.UtcNow.Date.AddMonths(-6),
            EndDate = DateTime.UtcNow.Date.AddMonths(6),
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });

        // 5. Tenancy Co-Tenants
        _coTenants.Add(new TenancyCoTenantEntity
        {
            TenancyId = DemoTenancyId,
            TenantId = DemoTenantId,
            RentSharePercentage = 100.00m
        });

        _invites.Add(new TenancyInviteEntity
        {
            Id = DemoInviteId,
            UnitId = DemoUnit2Id,
            LandlordId = DemoLandlordId,
            TenantEmail = "newtenant@example.com",
            TenantId = UnassignedTenantId,
            Status = "PENDING",
            StartDate = DateTime.UtcNow.Date,
            EndDate = DateTime.UtcNow.Date.AddYears(1),
            CreatedAt = DateTime.UtcNow.AddHours(-4)
        });

        // 6. Payments
        _payments.Add(new PaymentEntity
        {
            Id = Guid.NewGuid(),
            TenancyId = DemoTenancyId,
            PayerId = DemoTenantId,
            Amount = 14500.00m,
            Description = "Monthly Rent - Current Month",
            Method = "MANUAL_EFT",
            Status = "SETTLED",
            GatewayReference = "EFT-OCT-849201",
            PaidAt = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow.AddDays(-6)
        });

        _payments.Add(new PaymentEntity
        {
            Id = Guid.NewGuid(),
            TenancyId = DemoTenancyId,
            PayerId = DemoTenantId,
            Amount = 14500.00m,
            Description = "Monthly Rent - Previous Month",
            Method = "MANUAL_EFT",
            Status = "SETTLED",
            GatewayReference = "EFT-SEP-194820",
            PaidAt = DateTime.UtcNow.AddDays(-35),
            CreatedAt = DateTime.UtcNow.AddDays(-36)
        });

        // 7. Utility Meters
        _meters.Add(new UtilityMeterEntity
        {
            Id = DemoMeterId,
            UnitId = DemoUnit1Id,
            MeterNumber = "ELEC-CT-04829104",
            MeterType = "ELECTRICITY",
            VendorProvider = "City Power Prepaid (Conlog)"
        });

        var waterMeterId = Guid.NewGuid();
        _meters.Add(new UtilityMeterEntity
        {
            Id = waterMeterId,
            UnitId = DemoUnit1Id,
            MeterNumber = "WTR-CT-58193821",
            MeterType = "WATER",
            VendorProvider = "Aquaflow Sub-Metering"
        });

        // 8. Utility Tokens
        _tokens.Add(new UtilityTokenEntity
        {
            Id = Guid.NewGuid(),
            MeterId = DemoMeterId,
            PaymentId = _payments[0].Id,
            TokenCode = "4819-2049-5821-9302",
            UnitsPurchased = 245.50m,
            CreatedAt = DateTime.UtcNow.AddDays(-12)
        });

        _tokens.Add(new UtilityTokenEntity
        {
            Id = Guid.NewGuid(),
            MeterId = DemoMeterId,
            PaymentId = _payments[0].Id,
            TokenCode = "9182-4729-1049-8392",
            UnitsPurchased = 180.00m,
            CreatedAt = DateTime.UtcNow.AddDays(-40)
        });

        // 9. Work Orders
        _workOrders.Add(new WorkOrderEntity
        {
            Id = DemoWorkOrder1Id,
            UnitId = DemoUnit1Id,
            BuildingId = DemoBuilding1Id,
            LoggedById = DemoTenantId,
            AssignedContractorId = DemoContractorProfileId,
            Category = "PLUMBING",
            Priority = "HIGH",
            Status = "IN_PROGRESS",
            Title = "Kitchen Sink Trap Leak & Water Pressure Drop",
            Description = "The pipe joint beneath the double kitchen sink is dripping continuously into the lower cupboard. Water pressure in the mixer faucet has also dropped significantly.",
            ScheduledAccessTime = DateTime.UtcNow.AddDays(1).Date.AddHours(10),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        });

        _workOrders.Add(new WorkOrderEntity
        {
            Id = DemoWorkOrder2Id,
            UnitId = DemoUnit1Id,
            BuildingId = DemoBuilding1Id,
            LoggedById = DemoTenantId,
            AssignedContractorId = electricContractorId,
            Category = "ELECTRICAL",
            Priority = "MEDIUM",
            Status = "SCHEDULED",
            Title = "Patio Outdoor LED Spotlight Tripping Circuit",
            Description = "Whenever the patio light switch is toggled, the main lighting earth leakage breaker trips.",
            ScheduledAccessTime = DateTime.UtcNow.AddDays(3).Date.AddHours(14),
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        // 10. Contractor Quotes
        _quotes.Add(new ContractorQuoteEntity
        {
            Id = Guid.NewGuid(),
            WorkOrderId = DemoWorkOrder1Id,
            ContractorId = DemoContractorProfileId,
            LaborCost = 650.00m,
            MaterialCost = 420.00m,
            TotalAmount = 1070.00m,
            EstimatedHours = 2.5m,
            Notes = "Includes high-grade PVC P-trap replacement, rubber washer seals, and mixer aerator de-scaling.",
            Status = "APPROVED",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });

        // 11. Job Proof Media
        _proofMedia.Add(new JobProofMediaEntity
        {
            Id = Guid.NewGuid(),
            WorkOrderId = DemoWorkOrder1Id,
            UploadedById = DemoContractorUserId,
            ProofType = "BEFORE_PHOTO",
            MediaUrl = "https://images.unsplash.com/photo-1584622650111-993a426fbf0a?auto=format&fit=crop&w=800&q=80",
            Latitude = -33.9249m,
            Longitude = 18.4241m,
            CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(2)
        });

        // 12. Contractor Invoices
        _invoices.Add(new ContractorInvoiceEntity
        {
            Id = Guid.NewGuid(),
            WorkOrderId = DemoWorkOrder1Id,
            ContractorId = DemoContractorProfileId,
            InvoiceNumber = "INV-APEX-2026-004",
            TotalAmount = 1070.00m,
            IsApprovedByLandlord = false,
            PayoutStatus = "PENDING",
            CreatedAt = DateTime.UtcNow.AddHours(-3)
        });

        // 13. Inspections
        _inspections.Add(new InspectionEntity
        {
            Id = Guid.NewGuid(),
            TenancyId = DemoTenancyId,
            InspectionType = "MOVE_IN",
            ChecklistData = "{\"Front Door & Locks\":\"Pristine\",\"Walls & Paint\":\"Good - slight scuff behind bedroom door\",\"Kitchen Fixtures & Hob\":\"Functional & Clean\",\"Bathroom Plumbing\":\"Inspected & operational\",\"Windows & Balcony Latches\":\"Secure\",\"Keys & Access Tag Handover\":\"2 Sets received\"}",
            TenantSignedAt = DateTime.UtcNow.AddMonths(-6),
            LandlordSignedAt = DateTime.UtcNow.AddMonths(-6),
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });

        _inspections.Add(new InspectionEntity
        {
            Id = Guid.NewGuid(),
            TenancyId = DemoTenancyId,
            InspectionType = "ROUTINE",
            ChecklistData = "{\"Smoke Detectors\":\"Tested OK\",\"Geyser & Pressure Valve\":\"No leaks detected\",\"Cupboards & Shelves\":\"Clean and undamaged\",\"Tiling & Grouting\":\"Sound condition\"}",
            TenantSignedAt = null,
            LandlordSignedAt = DateTime.UtcNow.AddDays(-3),
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        });
    }

    // Users
    public Task<UserEntity?> GetUserByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_users.FirstOrDefault(u => u.Id == id));
        }
    }

    public Task<UserEntity?> GetUserByEmailAsync(string email)
    {
        lock (_lock)
        {
            return Task.FromResult(_users.FirstOrDefault(u => u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase)));
        }
    }

    public Task<UserEntity> CreateUserAsync(UserEntity user)
    {
        lock (_lock)
        {
            if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
            _users.RemoveAll(u => u.Id == user.Id || u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            _users.Add(user);
            return Task.FromResult(user);
        }
    }

    public Task<IReadOnlyList<UserEntity>> GetUsersByRoleAsync(string role)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<UserEntity>>(_users.Where(u => u.Role.Equals(role, StringComparison.OrdinalIgnoreCase)).ToList());
        }
    }

    // Buildings & Units
    public Task<IReadOnlyList<BuildingEntity>> GetBuildingsAsync(Guid landlordId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<BuildingEntity>>(
                _buildings.Where(building => building.LandlordId == landlordId).ToList());
        }
    }

    public Task CreateBuildingAsync(BuildingEntity building)
    {
        lock (_lock)
        {
            if (building.Id == 0) building.Id = _buildings.Count == 0 ? 1 : _buildings.Max(item => item.Id) + 1;
            building.Name = building.Name.Trim();
            building.Address = building.Address.Trim();
            building.CreatedAt = DateTime.UtcNow;
            _buildings.Add(building);
            return Task.CompletedTask;
        }
    }

    public Task DeleteBuildingAsync(Guid landlordId, int buildingId)
    {
        lock (_lock)
        {
            var building = _buildings.FirstOrDefault(item =>
                item.Id == buildingId && item.LandlordId == landlordId);
            if (building is null)
                throw new InvalidOperationException("The building could not be found for this landlord.");
            if (_units.Any(unit => unit.BuildingId == buildingId))
                throw new InvalidOperationException("Remove all units from this building before deleting it.");

            _buildings.Remove(building);
            return Task.CompletedTask;
        }
    }

    public Task<IReadOnlyList<UnitEntity>> GetUnitsAsync(Guid? landlordId = null)
    {
        lock (_lock)
        {
            var query = _units.AsEnumerable();
            if (landlordId.HasValue && landlordId.Value != Guid.Empty)
            {
                query = query.Where(u => u.LandlordId == landlordId.Value);
            }
            return Task.FromResult<IReadOnlyList<UnitEntity>>(query.ToList());
        }
    }

    public Task<UnitEntity?> GetUnitByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_units.FirstOrDefault(u => u.Id == id));
        }
    }

    public Task<UnitEntity> CreateUnitAsync(UnitEntity unit)
    {
        lock (_lock)
        {
            var building = _buildings.FirstOrDefault(item =>
                item.Id == unit.BuildingId && item.LandlordId == unit.LandlordId)
                ?? throw new InvalidOperationException("The selected building does not belong to this landlord.");
            if (unit.Id == Guid.Empty) unit.Id = Guid.NewGuid();
            unit.BuildingName = building.Name;
            unit.StreetAddress = building.Address;
            _units.Add(unit);
            return Task.FromResult(unit);
        }
    }

    public Task DeleteUnitAsync(Guid landlordId, Guid unitId)
    {
        lock (_lock)
        {
            var unit = _units.FirstOrDefault(item =>
                item.Id == unitId && item.LandlordId == landlordId);
            if (unit is null)
                throw new InvalidOperationException("The unit could not be found for this landlord.");

            var hasTenancies = _tenancies.Any(tenancy => tenancy.UnitId == unitId);
            var hasInvites = _invites.Any(invite => invite.UnitId == unitId);
            var hasMeters = _meters.Any(meter => meter.UnitId == unitId);
            var hasWorkOrders = _workOrders.Any(workOrder => workOrder.UnitId == unitId);
            if (hasTenancies || hasInvites || hasMeters || hasWorkOrders)
            {
                throw new InvalidOperationException(
                    "This unit has tenancy, invite, utility meter, or work-order history and cannot be deleted.");
            }

            _units.Remove(unit);
            return Task.CompletedTask;
        }
    }

    public Task<UnitEntity> UpdateUnitAsync(UnitEntity unit)
    {
        lock (_lock)
        {
            var idx = _units.FindIndex(u => u.Id == unit.Id);
            if (idx >= 0) _units[idx] = unit;
            else _units.Add(unit);
            return Task.FromResult(unit);
        }
    }

    // Tenancies
    public Task<TenancyEntity?> GetActiveTenancyForTenantAsync(Guid tenantId)
    {
        lock (_lock)
        {
            return Task.FromResult(_tenancies.FirstOrDefault(t => (t.PrimaryTenantId == tenantId || _coTenants.Any(c => c.TenancyId == t.Id && c.TenantId == tenantId)) && t.IsActive));
        }
    }

    public Task<IReadOnlyList<TenancyEntity>> GetTenanciesAsync(Guid? landlordId = null)
    {
        lock (_lock)
        {
            if (landlordId.HasValue && landlordId.Value != Guid.Empty)
            {
                var landlordUnitIds = _units.Where(u => u.LandlordId == landlordId.Value).Select(u => u.Id).ToHashSet();
                return Task.FromResult<IReadOnlyList<TenancyEntity>>(_tenancies.Where(t => landlordUnitIds.Contains(t.UnitId)).ToList());
            }
            return Task.FromResult<IReadOnlyList<TenancyEntity>>(_tenancies.ToList());
        }
    }

    public Task<TenancyEntity?> GetTenancyByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_tenancies.FirstOrDefault(t => t.Id == id));
        }
    }

    public Task<TenancyEntity> CreateTenancyAsync(TenancyEntity tenancy, IEnumerable<TenancyCoTenantEntity>? coTenants = null)
    {
        lock (_lock)
        {
            if (tenancy.Id == Guid.Empty) tenancy.Id = Guid.NewGuid();
            _tenancies.Add(tenancy);
            if (coTenants != null)
            {
                foreach (var c in coTenants)
                {
                    c.TenancyId = tenancy.Id;
                    _coTenants.Add(c);
                }
            }
            return Task.FromResult(tenancy);
        }
    }

    public Task<IReadOnlyList<TenancyCoTenantEntity>> GetCoTenantsAsync(Guid tenancyId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<TenancyCoTenantEntity>>(_coTenants.Where(c => c.TenancyId == tenancyId).ToList());
        }
    }

    public Task RemoveTenantFromUnitAsync(Guid landlordId, Guid tenancyId, Guid tenantId)
    {
        lock (_lock)
        {
            var tenancy = _tenancies.FirstOrDefault(item => item.Id == tenancyId);
            var unit = tenancy is null
                ? null
                : _units.FirstOrDefault(item =>
                    item.Id == tenancy.UnitId && item.LandlordId == landlordId);
            if (tenancy is null || unit is null)
                throw new InvalidOperationException("The tenancy could not be found for this landlord.");

            if (tenancy.PrimaryTenantId == tenantId)
            {
                tenancy.IsActive = false;
                tenancy.EndDate = DateTime.UtcNow.Date < tenancy.StartDate.Date
                    ? tenancy.StartDate.Date
                    : DateTime.UtcNow.Date;
                return Task.CompletedTask;
            }

            var coTenant = _coTenants.FirstOrDefault(item =>
                item.TenancyId == tenancyId && item.TenantId == tenantId);
            if (coTenant is null)
                throw new InvalidOperationException("The tenant is not assigned to this tenancy.");

            _coTenants.Remove(coTenant);
            return Task.CompletedTask;
        }
    }

    public Task<TenancyInviteEntity> InviteTenantToUnitAsync(Guid landlordId, Guid unitId, string tenantEmail, DateTime startDate, DateTime endDate)
    {
        lock (_lock)
        {
            return Task.FromResult(CreateInviteLocked(landlordId, unitId, tenantEmail, startDate, endDate));
        }
    }

    public Task<IReadOnlyList<TenancyInviteEntity>> GetInvitesForLandlordAsync(Guid landlordId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<TenancyInviteEntity>>(
                _invites.Where(i => i.LandlordId == landlordId).OrderByDescending(i => i.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<TenancyInviteEntity>> GetPendingInvitesForTenantAsync(Guid tenantId, string? email = null)
    {
        lock (_lock)
        {
            var cleanEmail = NormalizeEmail(email);
            return Task.FromResult<IReadOnlyList<TenancyInviteEntity>>(
                _invites.Where(i => i.Status == "PENDING" && MatchesTenant(i, tenantId, cleanEmail))
                    .OrderByDescending(i => i.CreatedAt)
                    .ToList());
        }
    }

    public Task<TenancyInviteEntity> AcceptInviteAsync(Guid inviteId, Guid tenantId)
    {
        lock (_lock)
        {
            return Task.FromResult(RespondToInviteLocked(inviteId, tenantId, accept: true));
        }
    }

    public Task<TenancyInviteEntity> DismissInviteAsync(Guid inviteId, Guid tenantId)
    {
        lock (_lock)
        {
            return Task.FromResult(RespondToInviteLocked(inviteId, tenantId, accept: false));
        }
    }

    private TenancyInviteEntity CreateInviteLocked(Guid landlordId, Guid unitId, string tenantEmail, DateTime startDate, DateTime endDate)
    {
        var email = NormalizeEmail(tenantEmail);
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("A tenant email address is required.");
        if (endDate.Date <= startDate.Date)
            throw new InvalidOperationException("Lease end date must be after the start date.");

        var unit = _units.FirstOrDefault(u => u.Id == unitId)
            ?? throw new InvalidOperationException("The selected unit could not be found.");
        if (unit.LandlordId != landlordId)
            throw new InvalidOperationException("You can only invite tenants to your own units.");

        var existingUser = _users.FirstOrDefault(u => string.Equals(NormalizeEmail(u.Email), email, StringComparison.Ordinal));
        if (existingUser != null && !string.Equals(existingUser.Role, "TENANT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("That email belongs to a non-tenant account.");

        if (_invites.Any(i => i.UnitId == unitId && i.Status == "PENDING" && NormalizeEmail(i.TenantEmail) == email))
            throw new InvalidOperationException("A pending invite already exists for this tenant and unit.");

        if (existingUser != null && TenantAlreadyOnUnitLocked(existingUser.Id, unitId))
            throw new InvalidOperationException("That tenant is already assigned to this unit.");

        var invite = new TenancyInviteEntity
        {
            Id = Guid.NewGuid(),
            UnitId = unitId,
            LandlordId = landlordId,
            TenantEmail = email,
            TenantId = existingUser?.Id,
            Status = "PENDING",
            StartDate = startDate.Date,
            EndDate = endDate.Date,
            CreatedAt = DateTime.UtcNow
        };
        _invites.Add(invite);
        return invite;
    }

    private TenancyInviteEntity RespondToInviteLocked(Guid inviteId, Guid tenantId, bool accept)
    {
        var invite = _invites.FirstOrDefault(i => i.Id == inviteId)
            ?? throw new InvalidOperationException("Invite not found.");
        if (!string.Equals(invite.Status, "PENDING", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This invite has already been answered.");

        var tenant = _users.FirstOrDefault(u => u.Id == tenantId)
            ?? throw new InvalidOperationException("Your tenant profile could not be found.");
        if (!MatchesTenant(invite, tenantId, tenant.Email))
            throw new InvalidOperationException("This invite was sent to a different email address.");

        invite.TenantId = tenantId;
        invite.RespondedAt = DateTime.UtcNow;
        invite.Status = accept ? "ACCEPTED" : "DISMISSED";

        if (accept)
            AssignTenantFromInviteLocked(invite, tenantId);

        return invite;
    }

    private void AssignTenantFromInviteLocked(TenancyInviteEntity invite, Guid tenantId)
    {
        if (TenantAlreadyOnUnitLocked(tenantId, invite.UnitId))
            return;

        var active = _tenancies.FirstOrDefault(t => t.UnitId == invite.UnitId && t.IsActive);
        if (active == null)
        {
            var tenancy = new TenancyEntity
            {
                Id = Guid.NewGuid(),
                UnitId = invite.UnitId,
                PrimaryTenantId = tenantId,
                StartDate = invite.StartDate,
                EndDate = invite.EndDate,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _tenancies.Add(tenancy);
            _coTenants.Add(new TenancyCoTenantEntity
            {
                TenancyId = tenancy.Id,
                TenantId = tenantId,
                RentSharePercentage = 100.00m
            });
            return;
        }

        if (!_coTenants.Any(c => c.TenancyId == active.Id && c.TenantId == tenantId))
        {
            _coTenants.Add(new TenancyCoTenantEntity
            {
                TenancyId = active.Id,
                TenantId = tenantId,
                RentSharePercentage = 0.00m
            });
        }
    }

    private bool TenantAlreadyOnUnitLocked(Guid tenantId, Guid unitId)
    {
        return _tenancies.Any(t =>
            t.UnitId == unitId &&
            t.IsActive &&
            (t.PrimaryTenantId == tenantId || _coTenants.Any(c => c.TenancyId == t.Id && c.TenantId == tenantId)));
    }

    private static bool MatchesTenant(TenancyInviteEntity invite, Guid tenantId, string? email)
    {
        if (invite.TenantId.HasValue && invite.TenantId.Value == tenantId)
            return true;
        var cleanEmail = NormalizeEmail(email);
        return !string.IsNullOrEmpty(cleanEmail) &&
               string.Equals(NormalizeEmail(invite.TenantEmail), cleanEmail, StringComparison.Ordinal);
    }

    private static string NormalizeEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    // Payments
    public Task<IReadOnlyList<PaymentEntity>> GetPaymentsForTenancyAsync(Guid tenancyId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<PaymentEntity>>(_payments.Where(p => p.TenancyId == tenancyId).OrderByDescending(p => p.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<PaymentEntity>> GetPaymentsForTenantAsync(Guid tenantId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<PaymentEntity>>(_payments.Where(p => p.PayerId == tenantId).OrderByDescending(p => p.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<PaymentEntity>> GetAllPaymentsAsync(Guid? landlordId = null)
    {
        lock (_lock)
        {
            var landlordTenancyIds = landlordId is { } ownerId && ownerId != Guid.Empty
                ? _tenancies
                    .Where(tenancy => _units.Any(unit => unit.Id == tenancy.UnitId && unit.LandlordId == ownerId))
                    .Select(tenancy => tenancy.Id)
                    .ToHashSet()
                : null;

            var payments = landlordTenancyIds is null
                ? _payments
                : _payments.Where(payment => landlordTenancyIds.Contains(payment.TenancyId));

            return Task.FromResult<IReadOnlyList<PaymentEntity>>(
                payments.OrderByDescending(payment => payment.CreatedAt).ToList());
        }
    }

    public Task<PaymentEntity> RecordPaymentAsync(PaymentEntity payment)
    {
        lock (_lock)
        {
            if (payment.Id == Guid.Empty) payment.Id = Guid.NewGuid();
            payment.PaidAt ??= DateTime.UtcNow;
            payment.Status = "SETTLED";
            _payments.Insert(0, payment);
            return Task.FromResult(payment);
        }
    }

    // Utilities
    public Task<IReadOnlyList<UtilityMeterEntity>> GetUtilityMetersForUnitAsync(Guid unitId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<UtilityMeterEntity>>(_meters.Where(m => m.UnitId == unitId).ToList());
        }
    }

    public Task<IReadOnlyList<UtilityTokenEntity>> GetUtilityTokensForMeterAsync(Guid meterId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<UtilityTokenEntity>>(_tokens.Where(t => t.MeterId == meterId).OrderByDescending(t => t.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<UtilityTokenEntity>> GetUtilityTokensForTenantAsync(Guid tenantId)
    {
        lock (_lock)
        {
            var tenancy = _tenancies.FirstOrDefault(t => t.PrimaryTenantId == tenantId && t.IsActive);
            if (tenancy == null) return Task.FromResult<IReadOnlyList<UtilityTokenEntity>>(new List<UtilityTokenEntity>());

            var unitMeters = _meters.Where(m => m.UnitId == tenancy.UnitId).Select(m => m.Id).ToHashSet();
            return Task.FromResult<IReadOnlyList<UtilityTokenEntity>>(_tokens.Where(t => unitMeters.Contains(t.MeterId)).OrderByDescending(t => t.CreatedAt).ToList());
        }
    }

    public Task<UtilityTokenEntity> PurchaseUtilityTokenAsync(Guid meterId, Guid payerId, decimal amount, decimal unitsPurchased)
    {
        lock (_lock)
        {
            var meter = _meters.FirstOrDefault(m => m.Id == meterId);
            var tenancy = _tenancies.FirstOrDefault(t => t.UnitId == (meter != null ? meter.UnitId : Guid.Empty));
            var tenancyId = tenancy?.Id ?? DemoTenancyId;

            var payment = new PaymentEntity
            {
                Id = Guid.NewGuid(),
                TenancyId = tenancyId,
                PayerId = payerId,
                Amount = amount,
                Description = $"Prepaid {(meter?.MeterType ?? "Utility")} Token Purchase",
                Method = "CARD",
                Status = "SETTLED",
                GatewayReference = $"TOKEN-TXN-{Random.Shared.Next(100000, 999999)}",
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            _payments.Insert(0, payment);

            // Generate 16-digit token code
            var tokenCode = $"{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}-{Random.Shared.Next(1000, 9999)}";

            var token = new UtilityTokenEntity
            {
                Id = Guid.NewGuid(),
                MeterId = meterId,
                PaymentId = payment.Id,
                TokenCode = tokenCode,
                UnitsPurchased = unitsPurchased,
                CreatedAt = DateTime.UtcNow
            };
            _tokens.Insert(0, token);
            return Task.FromResult(token);
        }
    }

    // Work Orders
    public Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForTenantAsync(Guid tenantId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<WorkOrderEntity>>(_workOrders.Where(w => w.LoggedById == tenantId).OrderByDescending(w => w.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForLandlordAsync(Guid landlordId)
    {
        lock (_lock)
        {
            var buildingIds = _buildings
                .Where(building => building.LandlordId == landlordId)
                .Select(building => building.Id)
                .ToHashSet();
            var landlordUnitIds = _units
                .Where(unit => unit.LandlordId == landlordId && buildingIds.Contains(unit.BuildingId))
                .Select(unit => unit.Id)
                .ToHashSet();
            var tenantIdsByUnit = _tenancies
                .Where(tenancy => landlordUnitIds.Contains(tenancy.UnitId))
                .GroupBy(tenancy => tenancy.UnitId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(tenancy => tenancy.PrimaryTenantId)
                        .Concat(group.SelectMany(tenancy => _coTenants
                            .Where(coTenant => coTenant.TenancyId == tenancy.Id)
                            .Select(coTenant => coTenant.TenantId)))
                        .ToHashSet());
            return Task.FromResult<IReadOnlyList<WorkOrderEntity>>(
                _workOrders.Where(workOrder =>
                        buildingIds.Contains(workOrder.BuildingId) &&
                        tenantIdsByUnit.TryGetValue(workOrder.UnitId, out var tenantIds) &&
                        tenantIds.Contains(workOrder.LoggedById))
                    .OrderByDescending(workOrder => workOrder.CreatedAt)
                    .ToList());
        }
    }

    public Task<IReadOnlyList<WorkOrderEntity>> GetWorkOrdersForContractorAsync(Guid contractorId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<WorkOrderEntity>>(_workOrders.Where(w => w.AssignedContractorId == contractorId).OrderByDescending(w => w.CreatedAt).ToList());
        }
    }

    public Task<WorkOrderEntity?> GetWorkOrderByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_workOrders.FirstOrDefault(w => w.Id == id));
        }
    }

    public Task<WorkOrderEntity?> GetContractorWorkOrderByDispatchPinAsync(Guid id, string pin)
    {
        lock (_lock)
        {
            var workOrder = _workOrders.FirstOrDefault(w => w.Id == id && w.AssignedContractorId.HasValue);
            var expectedPin = id.ToString("N")[..4];
            return Task.FromResult(
                workOrder != null && pin.Trim().Equals(expectedPin, StringComparison.OrdinalIgnoreCase)
                    ? workOrder
                    : null);
        }
    }

    public Task<WorkOrderEntity> CreateWorkOrderAsync(WorkOrderEntity workOrder)
    {
        lock (_lock)
        {
            var unit = _units.FirstOrDefault(item => item.Id == workOrder.UnitId)
                ?? throw new InvalidOperationException("The work order unit could not be found.");
            if (workOrder.Id == Guid.Empty) workOrder.Id = Guid.NewGuid();
            workOrder.BuildingId = unit.BuildingId;
            workOrder.Status = "LOGGED";
            _workOrders.Insert(0, workOrder);
            return Task.FromResult(workOrder);
        }
    }

    public Task<WorkOrderEntity> UpdateWorkOrderStatusAsync(Guid workOrderId, string newStatus)
    {
        lock (_lock)
        {
            var item = _workOrders.FirstOrDefault(w => w.Id == workOrderId);
            if (item != null)
            {
                item.Status = newStatus;
                return Task.FromResult(item);
            }
            throw new KeyNotFoundException($"Work order {workOrderId} not found.");
        }
    }

    public Task<WorkOrderEntity> AssignContractorAsync(Guid workOrderId, Guid contractorId)
    {
        lock (_lock)
        {
            var item = _workOrders.FirstOrDefault(w => w.Id == workOrderId);
            if (item != null)
            {
                item.AssignedContractorId = contractorId;
                if (item.Status == "LOGGED") item.Status = "CONTRACTOR_ASSIGNED";
                return Task.FromResult(item);
            }
            throw new KeyNotFoundException($"Work order {workOrderId} not found.");
        }
    }

    // Contractor Profiles
    public Task<IReadOnlyList<ContractorProfileEntity>> GetContractorProfilesAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContractorProfileEntity>>(_contractors.ToList());
        }
    }

    public Task<ContractorProfileEntity?> GetContractorProfileByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_contractors.FirstOrDefault(c => c.Id == id));
        }
    }

    public Task<ContractorProfileEntity?> GetContractorProfileByUserIdAsync(Guid userId)
    {
        lock (_lock)
        {
            return Task.FromResult(_contractors.FirstOrDefault(c => c.UserId == userId));
        }
    }

    public Task<ContractorProfileEntity> RegisterContractorProfileAsync(UserEntity user, ContractorProfileEntity profile)
    {
        lock (_lock)
        {
            if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
            user.Role = "CONTRACTOR";
            user.CreatedAt = DateTime.UtcNow;

            var existingUser = _users.FirstOrDefault(u => u.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase));
            if (existingUser != null)
            {
                user.Id = existingUser.Id;
            }
            else
            {
                _users.Add(user);
            }

            if (profile.Id == Guid.Empty) profile.Id = Guid.NewGuid();
            profile.UserId = user.Id;
            profile.CreatedAt = DateTime.UtcNow;

            var existingProfile = _contractors.FirstOrDefault(c => c.UserId == user.Id);
            if (existingProfile != null)
            {
                _contractors.Remove(existingProfile);
            }
            _contractors.Add(profile);

            return Task.FromResult(profile);
        }
    }

    public Task<ContractorProfileEntity?> VerifyContractorPinAsync(string pin)
    {
        lock (_lock)
        {
            var cleanPin = pin.Trim();
            // Match demo PINs or contractor ID prefixes or job codes
            if (cleanPin.Equals("7788", StringComparison.OrdinalIgnoreCase) ||
                cleanPin.Equals("CONTRACTOR123", StringComparison.OrdinalIgnoreCase) ||
                cleanPin.Equals("PIN-101", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult<ContractorProfileEntity?>(_contractors.FirstOrDefault());
            }

            // Check if PIN matches first characters of any work order or contractor ID
            var matchedWorkOrder = _workOrders.FirstOrDefault(w => w.Id.ToString().StartsWith(cleanPin, StringComparison.OrdinalIgnoreCase) ||
                                                                   w.Id.ToString().EndsWith(cleanPin, StringComparison.OrdinalIgnoreCase));
            if (matchedWorkOrder != null && matchedWorkOrder.AssignedContractorId.HasValue)
            {
                return Task.FromResult(_contractors.FirstOrDefault(c => c.Id == matchedWorkOrder.AssignedContractorId.Value));
            }

            // Fallback for demo testing: if pin length >= 4, grant apex contractor
            if (cleanPin.Length >= 4)
            {
                return Task.FromResult<ContractorProfileEntity?>(_contractors.FirstOrDefault());
            }

            return Task.FromResult<ContractorProfileEntity?>(null);
        }
    }

    // Quotes
    public Task<IReadOnlyList<ContractorQuoteEntity>> GetQuotesForWorkOrderAsync(Guid workOrderId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContractorQuoteEntity>>(_quotes.Where(q => q.WorkOrderId == workOrderId).OrderByDescending(q => q.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<ContractorQuoteEntity>> GetQuotesForContractorAsync(Guid contractorId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContractorQuoteEntity>>(_quotes.Where(q => q.ContractorId == contractorId).OrderByDescending(q => q.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<ContractorQuoteEntity>> GetAllQuotesAsync()
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContractorQuoteEntity>>(_quotes.OrderByDescending(q => q.CreatedAt).ToList());
        }
    }

    public Task<ContractorQuoteEntity> SubmitQuoteAsync(ContractorQuoteEntity quote)
    {
        lock (_lock)
        {
            if (quote.Id == Guid.Empty) quote.Id = Guid.NewGuid();
            quote.TotalAmount = quote.LaborCost + quote.MaterialCost;
            quote.Status = "SUBMITTED";
            _quotes.Insert(0, quote);
            return Task.FromResult(quote);
        }
    }

    public Task<ContractorQuoteEntity> UpdateQuoteStatusAsync(Guid quoteId, string status)
    {
        lock (_lock)
        {
            var quote = _quotes.FirstOrDefault(q => q.Id == quoteId);
            if (quote != null)
            {
                quote.Status = status;
                return Task.FromResult(quote);
            }
            throw new KeyNotFoundException($"Quote {quoteId} not found.");
        }
    }

    // Job Proof Media
    public Task<IReadOnlyList<JobProofMediaEntity>> GetProofMediaForWorkOrderAsync(Guid workOrderId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<JobProofMediaEntity>>(_proofMedia.Where(p => p.WorkOrderId == workOrderId).OrderByDescending(p => p.CreatedAt).ToList());
        }
    }

    public Task<JobProofMediaEntity> UploadProofMediaAsync(JobProofMediaEntity media)
    {
        lock (_lock)
        {
            if (media.Id == Guid.Empty) media.Id = Guid.NewGuid();
            _proofMedia.Insert(0, media);
            return Task.FromResult(media);
        }
    }

    // Invoices
    public Task<IReadOnlyList<ContractorInvoiceEntity>> GetInvoicesForWorkOrderAsync(Guid workOrderId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContractorInvoiceEntity>>(_invoices.Where(i => i.WorkOrderId == workOrderId).OrderByDescending(i => i.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<ContractorInvoiceEntity>> GetInvoicesForContractorAsync(Guid contractorId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContractorInvoiceEntity>>(_invoices.Where(i => i.ContractorId == contractorId).OrderByDescending(i => i.CreatedAt).ToList());
        }
    }

    public Task<IReadOnlyList<ContractorInvoiceEntity>> GetAllInvoicesAsync(Guid? landlordId = null)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<ContractorInvoiceEntity>>(_invoices.OrderByDescending(i => i.CreatedAt).ToList());
        }
    }

    public Task<ContractorInvoiceEntity> SubmitInvoiceAsync(ContractorInvoiceEntity invoice)
    {
        lock (_lock)
        {
            if (invoice.Id == Guid.Empty) invoice.Id = Guid.NewGuid();
            invoice.IsApprovedByLandlord = false;
            invoice.PayoutStatus = "PENDING";
            _invoices.Insert(0, invoice);
            return Task.FromResult(invoice);
        }
    }

    public Task<ContractorInvoiceEntity> ApproveInvoiceAsync(Guid invoiceId, bool isApproved)
    {
        lock (_lock)
        {
            var invoice = _invoices.FirstOrDefault(i => i.Id == invoiceId);
            if (invoice != null)
            {
                invoice.IsApprovedByLandlord = isApproved;
                invoice.PayoutStatus = isApproved ? "SETTLED" : "FAILED";
                return Task.FromResult(invoice);
            }
            throw new KeyNotFoundException($"Invoice {invoiceId} not found.");
        }
    }

    // Inspections
    public Task<IReadOnlyList<InspectionEntity>> GetInspectionsForTenancyAsync(Guid tenancyId)
    {
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<InspectionEntity>>(_inspections.Where(i => i.TenancyId == tenancyId).OrderByDescending(i => i.CreatedAt).ToList());
        }
    }

    public Task<InspectionEntity?> GetInspectionByIdAsync(Guid id)
    {
        lock (_lock)
        {
            return Task.FromResult(_inspections.FirstOrDefault(i => i.Id == id));
        }
    }

    public Task<InspectionEntity> CreateInspectionAsync(InspectionEntity inspection)
    {
        lock (_lock)
        {
            if (inspection.Id == Guid.Empty) inspection.Id = Guid.NewGuid();
            _inspections.Insert(0, inspection);
            return Task.FromResult(inspection);
        }
    }

    public Task<InspectionEntity> SignInspectionAsync(Guid inspectionId, bool isTenant)
    {
        lock (_lock)
        {
            var inspection = _inspections.FirstOrDefault(i => i.Id == inspectionId);
            if (inspection != null)
            {
                if (isTenant) inspection.TenantSignedAt = DateTime.UtcNow;
                else inspection.LandlordSignedAt = DateTime.UtcNow;
                return Task.FromResult(inspection);
            }
            throw new KeyNotFoundException($"Inspection {inspectionId} not found.");
        }
    }
}
