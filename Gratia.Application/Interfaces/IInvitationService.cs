using Gratia.Application.DTOs.Invitation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface IInvitationService
    {
        Task<InvitationDto> InviteUserAsync(Guid companyId, Guid invitedByUserId, InviteUserDto inviteUserDto);
        Task<InvitationDto> GetInvitationByTokenAsync(string token);
        Task<bool> AcceptInvitationAsync(AcceptInvitationDto acceptInvitationDto);
        Task<IEnumerable<InvitationDto>> GetPendingInvitationsAsync(Guid companyId);
        Task<bool> ResendInvitationAsync(Guid invitationId);
        Task<bool> CancelInvitationAsync(Guid invitationId);
    }
}
