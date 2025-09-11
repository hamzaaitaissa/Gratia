using Gratia.Application.DTOs.Invitation;
using Gratia.Application.Interfaces;
using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

        public async Task<InvitationDto> InviteUserAsync(Guid companyId, Guid invitedByUserId, InviteUserDto inviteUserDto)
        {
            var user = await _userRepository.GetByEmailAsync(inviteUserDto.Email);  
            if (user != null)
                throw new InvalidOperationException("User with this email already exists");
            var existingInvitation = await _invitationRepository.GetByEmailAndCompanyIdAsync(inviteUserDto.Email, companyId);
            if (existingInvitation != null && existingInvitation.IsValid)
                throw new InvalidOperationException("An active invitation already exists for this email in the company");
            var token = GenerateInvitationToken();
            var expiresAt = DateTime.UtcNow.AddDays(7);
            var invitation = new CompanyInvitation(
                inviteUserDto.Email,
                token,
                expiresAt,
                inviteUserDto.Role,
                companyId,
                invitedByUserId);
            var createdInvitation  = await _invitationRepository.CreateAsync(invitation);
            var invitationLink = $"https://yourapp.com/accept-invitation?token={token}";
            await _emailService.SendInvitationEmailAsync(inviteUserDto.Email, token, companyId);
            return new InvitationDto
            {
                Id = createdInvitation.Id,
                Email = createdInvitation.Email,
                InvitationToken = createdInvitation.InvitationToken,
                ExpiresAt = createdInvitation.ExpiresAt,
                IsUsed = createdInvitation.IsUsed,
                InvitedRole = createdInvitation.InvitedRole,
                CompanyName = createdInvitation.Company?.Name ?? "",
                InvitedByUserName = createdInvitation.InvitedByUser?.FullName ?? "",
                CreatedAt = createdInvitation.CreatedDate,
                IsExpired = createdInvitation.IsExpired,
                IsValid = createdInvitation.IsValid
            };
        }

        public Task<bool> ResendInvitationAsync(Guid invitationId)
        {
            throw new NotImplementedException();
        }
        private string GenerateInvitationToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "");
        }
    }
}
