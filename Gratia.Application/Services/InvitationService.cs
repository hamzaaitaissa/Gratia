using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
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
        private readonly IEmailService _emailService;
        private readonly IPasswordHashingService _passwordHashingService;

        public InvitationService(
                   IInvitationRepository invitationRepository,
                   IUserRepository userRepository,
                   IEmailService emailService,
                   IPasswordHashingService passwordHashingService)
        {
            _invitationRepository = invitationRepository;
            _userRepository = userRepository;
            _emailService = emailService;
            _passwordHashingService = passwordHashingService;
        }


        public async Task<bool> AcceptInvitationAsync(AcceptInvitationDto acceptInvitationDto)
        {
            var invitation = await _invitationRepository.GetByTokenAsync(acceptInvitationDto.Token);
            if (invitation == null || !invitation.IsValid)
                throw new InvalidOperationException("Invalid or expired invitation");
            
            var existingUser = await _userRepository.GetByEmailAsync(invitation.Email);
            if (existingUser != null)
                throw new InvalidOperationException("User with this email already exists");

            var hashedPassword = _passwordHashingService.HashPassword(acceptInvitationDto.Password);

            var newUser = new User(
                acceptInvitationDto.FullName,
                invitation.Email,
                hashedPassword,
                acceptInvitationDto.JobTitle,
                invitation.InvitedRole,
                25, 
                0,  
                invitation.CompanyId);

            await _userRepository.AddAsync(newUser);

            invitation.IsUsed = true;
            await _invitationRepository.UpdateAsync(invitation);

            return true;
        }

        public async Task<bool> CancelInvitationAsync(Guid invitationId)
        {
            return await _invitationRepository.DeleteAsync(invitationId);
        }

        public async Task<InvitationDto> GetInvitationByTokenAsync(string token)
        {
            var invitation = await _invitationRepository.GetByTokenAsync(token);
            if(invitation == null)
            {
                return null;
            }
            return new InvitationDto
            {
                Id = invitation.Id,
                Email = invitation.Email,
                InvitationToken = invitation.InvitationToken,
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

        public async Task<IEnumerable<InvitationDto>> GetPendingInvitationsAsync(Guid companyId)
        {
            var invitations =  await _invitationRepository.GetPendingInvitationsByCompanyIdAsync(companyId);
            List<InvitationDto> invitesList = new List<InvitationDto>();
            foreach(var invitation in invitations)
            {
                invitesList.Add(new InvitationDto
                {
                    Id = invitation.Id,
                    Email = invitation.Email,
                    InvitationToken = invitation.InvitationToken,
                    ExpiresAt = invitation.ExpiresAt,
                    IsUsed = invitation.IsUsed,
                    InvitedRole = invitation.InvitedRole,
                    CompanyName = invitation.Company?.Name ?? "",
                    InvitedByUserName = invitation.InvitedByUser?.FullName ?? "",
                    CreatedAt = invitation.CreatedDate,
                    IsExpired = invitation.IsExpired,
                    IsValid = invitation.IsValid
                });
            }
            return invitesList;
        }

        public Task<InvitationDto> InviteUserAsync(Guid companyId, Guid invitedByUserId, InviteUserDto inviteUserDto)
        {
            
        }

        public Task<bool> ResendInvitationAsync(Guid invitationId)
        {
            throw new NotImplementedException();
        }
    }
}
