using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Interfaces;
using Gratia.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Services
{
    public class InvitationService : IInvitationService
    {
        private readonly IInvitationRepository _invitationRepository;
        private readonly IUserRepository _userRepository;
        private readonly EmailService _emailService;
        private readonly IPasswordHashingService _passwordHashingService;

        public InvitationService(
                   IInvitationRepository invitationRepository,
                   IUserRepository userRepository,
                   EmailService emailService,
                   IPasswordHashingService passwordHashingService)
        {
            _invitationRepository = invitationRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _passwordHashingService = passwordHashingService;
        }


        public Task<bool> AcceptInvitationAsync(AcceptInvitationDto acceptInvitationDto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CancelInvitationAsync(Guid invitationId)
        {
            throw new NotImplementedException();
        }

        public Task<InvitationDto> GetInvitationByTokenAsync(string token)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<InvitationDto>> GetPendingInvitationsAsync(Guid companyId)
        {
            throw new NotImplementedException();
        }

        public Task<InvitationDto> InviteUserAsync(Guid companyId, Guid invitedByUserId, InviteUserDto inviteUserDto)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ResendInvitationAsync(Guid invitationId)
        {
            throw new NotImplementedException();
        }
    }
}
