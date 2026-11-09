using Gratia.Application.DTOs.UserDTO;
using Gratia.Domain.Entities;

namespace Gratia.Application.Mappings;

public static class UserMappings
{
    public static ReadUserDto ToReadDto(this User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Email = user.Email,
        JobTitle = user.JobTitle,
        Role = user.Role,
        NumberOfPointsAcquired = user.NumberOfPointsAcquired,
        NumberOfPointsAvailable = user.NumberOfPointsAvailable,
        CompanyId = user.CompanyId,
        DeactivatedAt = user.DeactivatedAt
    };
}
