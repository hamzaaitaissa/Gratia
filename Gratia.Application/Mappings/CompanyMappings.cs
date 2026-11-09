using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Domain.Entities;

namespace Gratia.Application.Mappings;

public static class CompanyMappings
{
    public static ReadCompanyDto ToReadDto(this Company company) => new()
    {
        Id = company.Id,
        Name = company.Name,
        PrimaryColor = company.PrimaryColor,
        SecondaryColor = company.SecondaryColor,
        LogoUrl = company.LogoUrl,
        Domain = company.Domain,
        IsDomainVerified = company.IsDomainVerified
    };
}
