using System;
using System.ComponentModel.DataAnnotations;
using RentLog.Models.Entities;

namespace RentLog.Models;

public class SignUpViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, ErrorMessage = "Name is too long.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Please select an account role.")]
    public string Role { get; set; } = "TENANT";

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    public string Password { get; set; } = string.Empty;
}

public class ContractorLoginViewModel
{
    [Required(ErrorMessage = "Temporary password or job PIN is required.")]
    public string JobPin { get; set; } = string.Empty;

    public string? CompanyOrEmail { get; set; }
}

public class CreateWorkOrderViewModel
{
    [Required(ErrorMessage = "Please select a unit.")]
    public Guid UnitId { get; set; }

    [Required(ErrorMessage = "Category is required.")]
    public string Category { get; set; } = "PLUMBING";

    [Required(ErrorMessage = "Priority is required.")]
    public string Priority { get; set; } = "MEDIUM";

    [Required(ErrorMessage = "Issue title is required.")]
    [StringLength(100, ErrorMessage = "Title must be under 100 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(2000, ErrorMessage = "Description must be under 2000 characters.")]
    public string Description { get; set; } = string.Empty;

    public DateTime? ScheduledAccessTime { get; set; }

    public List<string> MediaUrls { get; set; } = new();
}

public class SubmitQuoteViewModel
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required(ErrorMessage = "Labor cost is required.")]
    [Range(0, 1000000, ErrorMessage = "Labor cost must be positive.")]
    public decimal LaborCost { get; set; }

    [Required(ErrorMessage = "Material cost is required.")]
    [Range(0, 1000000, ErrorMessage = "Material cost must be positive.")]
    public decimal MaterialCost { get; set; }

    [Range(0, 500, ErrorMessage = "Estimated hours must be valid.")]
    public decimal? EstimatedHours { get; set; }

    public string? Notes { get; set; }
}

public class SubmitInvoiceViewModel
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required(ErrorMessage = "Invoice number is required.")]
    public string InvoiceNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Total amount is required.")]
    [Range(1, 1000000, ErrorMessage = "Total amount must be greater than zero.")]
    public decimal TotalAmount { get; set; }
}

public class UploadProofMediaViewModel
{
    [Required]
    public Guid WorkOrderId { get; set; }

    [Required(ErrorMessage = "Proof type is required.")]
    public string ProofType { get; set; } = "BEFORE_PHOTO";

    [Required(ErrorMessage = "Media URL or file is required.")]
    public string MediaUrl { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class MakePaymentViewModel
{
    [Required]
    public Guid TenancyId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(1, 1000000, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    public string Description { get; set; } = "Monthly Rent Payment";

    [Required(ErrorMessage = "Payment method is required.")]
    public string Method { get; set; } = "MANUAL_EFT";
}

public class PurchaseTokenViewModel
{
    [Required]
    public Guid MeterId { get; set; }

    [Required(ErrorMessage = "Payment amount is required.")]
    [Range(10, 50000, ErrorMessage = "Payment amount must be at least R10.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Units is required.")]
    [Range(1, 10000, ErrorMessage = "Units purchased must be positive.")]
    public decimal UnitsPurchased { get; set; }
}

public class InviteTenantViewModel
{
    [Required(ErrorMessage = "Please select a unit.")]
    public Guid UnitId { get; set; }

    [Required(ErrorMessage = "Tenant email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string TenantEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Lease start date is required.")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Lease end date is required.")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);
}

public class CreateUnitViewModel
{
    [Required(ErrorMessage = "Unit number is required.")]
    public string UnitNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select a building.")]
    public int BuildingId { get; set; }

    [Required(ErrorMessage = "Monthly rent is required.")]
    [Range(1, 1000000, ErrorMessage = "Rent must be greater than zero.")]
    public decimal MonthlyRent { get; set; }

    [Required(ErrorMessage = "Deposit amount is required.")]
    [Range(0, 1000000, ErrorMessage = "Deposit cannot be negative.")]
    public decimal DepositAmount { get; set; }
}

public class CreateBuildingViewModel
{
    [Required(ErrorMessage = "Building name is required.")]
    [StringLength(150, ErrorMessage = "Building name must be 150 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Street address is required.")]
    [StringLength(300, ErrorMessage = "Address must be 300 characters or fewer.")]
    public string Address { get; set; } = string.Empty;
}

public class AddContractorViewModel
{
    [Required(ErrorMessage = "Contractor or company contact name is required.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Company or trading name is required.")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Trade category is required.")]
    public string Trade { get; set; } = "PLUMBING";

    public string? TaxVatNumber { get; set; }

    public string? BankAccountDetails { get; set; }

    public bool IsVerified { get; set; } = true;
}
