using Gratia.Domain.Entities;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Points.Commands;

/// <summary>
/// Refills points to give in every company whose reset day has come since its last refill (#19), each to its own
/// allowance. A reset that was missed (the API was down on the day) happens on the next run. Safe to send as often, and
/// from as many API instances, as you like: each company's period is claimed in the database first, so it's refilled
/// once. A company whose refill fails is logged and tried again on the next run, and the others go ahead (#77).
/// Returns the number of companies refilled.
/// </summary>
public sealed record ResetDuePointsCommand(DateTime Now) : ICommand<int>;

public sealed class ResetDuePointsCommandHandler(
    ICompanyRepository companyRepository,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    ILogger<ResetDuePointsCommandHandler> logger)
    : ICommandHandler<ResetDuePointsCommand, int>
{
    public async ValueTask<int> Handle(ResetDuePointsCommand command, CancellationToken cancellationToken)
    {
        var refilled = 0;
        foreach (var company in await companyRepository.GetAllForPointsResetAsync(cancellationToken))
        {
            var periodStart = company.PointsPeriodStart(command.Now);
            if (company.PointsPeriodStartedAt >= periodStart)
                continue; // the usual case: this period is already done

            // A failure in one company (a lock timeout, a deadlock) used to end the run, so the companies after it
            // waited for the next one, and a failure that kept happening held up the same companies every time
            try
            {
                if (await TryRefillAsync(company, periodStart, cancellationToken))
                    refilled++;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                // Rolled back with its claim, so the next run tries again
                logger.LogError(ex, "Refilling company {CompanyId} for the period starting {PeriodStart:u} failed; the next run tries again",
                    company.Id, periodStart);
            }
        }

        return refilled;
    }

    // False when another instance got there first, or when it's the company's first run
    private async Task<bool> TryRefillAsync(Company company, DateTime periodStart, CancellationToken cancellationToken)
    {
        // The claim and the refill commit together, so a failed refill is retried on the next run
        await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        if (!await companyRepository.TryStartPointsPeriodAsync(company.Id, periodStart, cancellationToken))
            return false;

        // The first time the job sees a company it only records the period: its members got the allowance when
        // they joined, and refilling now would hand out an extra month's points mid-period
        var refill = company.PointsPeriodStartedAt is not null;
        var users = refill
            ? await userRepository.ResetAvailablePointsAsync(company.Id, company.MonthlyPointsAllowance, cancellationToken)
            : 0;

        await dbTransaction.CommitAsync(cancellationToken);

        // Only once it's committed: a failed commit is logged as a failure instead
        if (refill)
            logger.LogInformation("Refilled {Users} users of company {CompanyId} to {Allowance} points for the period starting {PeriodStart:u}",
                users, company.Id, company.MonthlyPointsAllowance, periodStart);
        return refill;
    }
}
