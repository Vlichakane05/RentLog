using RentLog.Models;

namespace RentLog.Services;

public interface IPropertyService
{
    Task<IReadOnlyList<RentalProperty>> GetPropertiesAsync(string landlordId);
    Task<RentalProperty> CreatePropertyAsync(string landlordId, string name, string address);
    Task<RentalUnit> CreateUnitAsync(string landlordId, string propertyId, string unitNumber, decimal monthlyRent);
    Task EndTenantTenancyAsync(string landlordId, string unitId, string tenantId);
    Task<IReadOnlyList<ContractorJob>> GetContractorJobsAsync(string landlordId);
}
