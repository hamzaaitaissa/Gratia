using Gratia.Application.Settings;
using Gratia.Domain.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gratia.Application.Features.Points.Commands;

// Sets everyone's points-to-give back to the monthly allowance. Returns the number of users updated.
public sealed record ResetMonthlyPointsCommand : ICommand<int>;

public sealed class ResetMonthlyPointsCommandHandler(
    IUserRepository userRepository,
    IOptions<PointsOptions> pointsOptions,
    ILogger<ResetMonthlyPointsCommandHandler> logger)
    : ICommandHandler<ResetMonthlyPointsCommand, int>
{
    public async ValueTask<int> Handle(ResetMonthlyPointsCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting the monthly reset process");

        // A single UPDATE that runs immediately (outside the unit of work), so every user is reset or none is
        var updatedCount = await userRepository.ResetAvailablePointsAsync(pointsOptions.Value.MonthlyAllowance);

        logger.LogInformation("Completed monthly point reset. Affected users count: {UpdatedCount}", updatedCount);
        return updatedCount;
    }
}
