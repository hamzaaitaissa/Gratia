using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Domain.Entities;

namespace Gratia.Application.Mappings;

public static class CompanyMappings
{
    public static ReadCompanyDto ToReadDto(this Company company)
    {
        var now = DateTime.UtcNow;
        return new()
        {
            Id = company.Id,
            Name = company.Name,
            PrimaryColor = company.PrimaryColor,
            SecondaryColor = company.SecondaryColor,
            LogoUrl = company.LogoUrl,
            Domain = company.Domain,
            IsDomainVerified = company.IsDomainVerified,
            MonthlyPointsAllowance = company.MonthlyPointsAllowance,
            PointsResetDay = company.PointsResetDay,
            LastPointsResetAt = company.PointsPeriodStart(now),
            NextPointsResetAt = company.NextPointsReset(now)
        };
    }
}
