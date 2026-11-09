using Gratia.Application.Common;
using Gratia.Application.DTOs.CompanyDTO;
using Gratia.Application.Exceptions;
using Gratia.Application.Features.Invitations;
using Gratia.Application.Interfaces;
using Gratia.Application.Settings;
using Gratia.Domain.Entities;
using Gratia.Domain.Enum;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Options;

namespace Gratia.Application.Features.Companies.Commands;

/// <summary>
/// Self-serve signup (TASKS.md #1 and #49). Creates a provisional company and a founding invitation with the Admin
/// role. The founder becomes the first admin by accepting it, which also proves they control the mailbox.
/// </summary>
public sealed record RegisterCompanyCommand(CompanySignupDto Signup) : ICommand;

public sealed class RegisterCompanyCommandHandler(
    ICompanyRepository companyRepository,
    IUserRepository userRepository,
    IInvitationRepository invitationRepository,
    IUnitOfWork unitOfWork,
    IEmailService emailService,
    IOptions<OnboardingOptions> onboardingOptions)
    : ICommandHandler<RegisterCompanyCommand>
{
    public async ValueTask<Unit> Handle(RegisterCompanyCommand command, CancellationToken cancellationToken)
    {
        var email = EmailAddresses.Normalize(command.Signup.Email);
        var companyName = command.Signup.CompanyName.Trim();
        var founderName = command.Signup.FullName.Trim();

        var domain = EmailAddresses.DomainOf(email);
        var isFreeEmail = FreeEmailDomains.Contains(domain);
        if (isFreeEmail && !onboardingOptions.Value.AllowFreeEmailDomains)
            throw new BusinessRuleException("Sign up with your work email address.");

        // From here on the caller always gets the same answer ("check your email"), and only the mailbox owner
        // learns what happened. So signup can't be used to find out who has an account or which companies use Gratia.

        if (await userRepository.GetByEmailAsync(email) is not null)
        {
            await emailService.SendAccountExistsEmailAsync(email, cancellationToken);
            return Unit.Value;
        }

        // A free email domain is shared by strangers, so a workspace founded with one isn't tied to a domain
        var companyDomain = isFreeEmail ? null : domain;
        var company = companyDomain is null ? null : await companyRepository.GetByDomainAsync(companyDomain);

        if (company is null)
        {
            company = new Company(companyName, string.Empty, Company.DefaultPrimaryColor, Company.DefaultSecondaryColor)
            {
                Domain = companyDomain
            };
            companyRepository.Add(company);
        }
        else if (await userRepository.AnyInCompanyAsync(company.Id))
        {
            // The domain's workspace is up and running: joining it takes an invitation from one of its admins
            await emailService.SendWorkspaceExistsEmailAsync(email, company.Name, cancellationToken);
            return Unit.Value;
        }
        else
        {
            // Nobody has finished setting this workspace up yet
            var ownInvitation = await invitationRepository.GetByEmailAndCompanyIdAsync(email, company.Id);
            if (ownInvitation is not null)
            {
                // The same founder signing up again: a fresh link replaces the old one
                company.Name = companyName;
                ownInvitation.InvitationToken = InvitationTokens.Generate();
                ownInvitation.ExpiresAt = DateTime.UtcNow.Add(InvitationTokens.Lifetime);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await emailService.SendCompanySetupEmailAsync(email, founderName, company.Name, ownInvitation.InvitationToken, cancellationToken);
                return Unit.Value;
            }

            if (await invitationRepository.HasActiveInvitationsAsync(company.Id))
            {
                // A colleague's setup link is still valid, so they get the chance to finish first
                await emailService.SendWorkspaceExistsEmailAsync(email, company.Name, cancellationToken);
                return Unit.Value;
            }

            // Abandoned: the earlier founder's link expired unused, so this signup takes the workspace over
            company.Name = companyName;
        }

        var invitation = new CompanyInvitation(
            email,
            InvitationTokens.Generate(),
            DateTime.UtcNow.Add(InvitationTokens.Lifetime),
            UserRole.Admin,
            company.Id,
            invitedByUserId: null)
        {
            Company = company
        };
        invitationRepository.Add(invitation);

        // The company and its founding invitation are saved together
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // If sending fails the founder gets a 503 (EmailDeliveryException), and the company and invitation stay: signing
        // up again sends a new link, and once that one expires unused anyone from the domain can take the workspace over.
        // Every branch answers 503 when email is down, so this doesn't give anything away either.
        await emailService.SendCompanySetupEmailAsync(email, founderName, company.Name, invitation.InvitationToken, cancellationToken);
        return Unit.Value;
    }
}
