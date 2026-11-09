using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Gratia.Application.Features.Points.Commands;

/// <summary>
/// Refills points to give in every company whose reset day has come since its last refill (#19), each to its own
/// allowance. A reset that was missed (the API was down on the day) happens on the next run. Safe to send as often, and
/// from as many API instances, as you like: each company's period is claimed in the database first, so it's refilled
/// once. Returns the number of companies refilled.
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

            // The claim and the refill commit together, so a failed refill is retried on the next run
            await using var dbTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            if (!await companyRepository.TryStartPointsPeriodAsync(company.Id, periodStart, cancellationToken))
                continue; // another instance got there first

            // The first time the job sees a company it only records the period: its members got the allowance when
            // they joined, and refilling now would hand out an extra month's points mid-period
            if (company.PointsPeriodStartedAt is not null)
            {
                var users = await userRepository.ResetAvailablePointsAsync(company.Id, company.MonthlyPointsAllowance, cancellationToken);
                logger.LogInformation("Refilled {Users} users of company {CompanyId} to {Allowance} points for the period starting {PeriodStart:u}",
                    users, company.Id, company.MonthlyPointsAllowance, periodStart);
                refilled++;
            }

            await dbTransaction.CommitAsync(cancellationToken);
        }

        return refilled;
    }
}
