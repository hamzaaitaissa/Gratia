using Gratia.Application.Exceptions;
using Gratia.Domain.Repositories;

namespace Gratia.Application.Features.Users;

// A company always keeps an active admin (#63): only admins can invite people, manage members and change the company.
// For the commands that take an admin away (demoting or deactivating one), inside their transaction and before the
// change is saved.
internal static class LastAdmin
{
    public const string Message = "A company needs at least one admin. Make someone else an admin first.";

    public static async Task EnsureAnotherRemainsAsync(ICompanyRepository companyRepository, IUserRepository userRepository,
        Guid companyId, Guid leavingAdminId, CancellationToken cancellationToken)
    {
        // Two admins demoting or deactivating each other at the same moment would each still see the other one. With
        // the company's row locked first, the second waits for the first to commit, then sees its result.
        await companyRepository.LockAsync(companyId, cancellationToken);
        if (!await userRepository.HasOtherActiveAdminAsync(companyId, leavingAdminId, cancellationToken))
            throw new BusinessRuleException(Message);
    }
}
