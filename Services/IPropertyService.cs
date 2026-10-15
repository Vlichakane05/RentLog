using RentLog.Models;

namespace RentLog.Services;

public interface IPropertyService
{
    Task<IReadOnlyList<RentalProperty>> GetPropertiesAsync(string landlordId);
    Task<IReadOnlyList<ContractorJob>> GetContractorJobsAsync(string landlordId);
}
