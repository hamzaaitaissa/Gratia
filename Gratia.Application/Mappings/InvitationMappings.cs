using Gratia.Application.DTOs.Invitation;
using Gratia.Domain.Entities;

namespace Gratia.Application.Mappings;

public static class InvitationMappings
{
    public static InvitationDto ToDto(this CompanyInvitation invitation) => new()
    {
        Id = invitation.Id,
        Email = invitation.Email,
        ExpiresAt = invitation.ExpiresAt,
        IsUsed = invitation.IsUsed,
        InvitedRole = invitation.InvitedRole,
        CompanyName = invitation.Company?.Name ?? "",
        InvitedByUserName = invitation.InvitedByUser?.FullName ?? "",
        CreatedAt = invitation.CreatedDate,
        IsExpired = invitation.IsExpired,
        IsValid = invitation.IsValid
    };
}
