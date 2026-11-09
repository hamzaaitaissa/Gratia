using Gratia.Application.Features.Points.Commands;
using Mediator;

namespace Gratia.API.Services
{
    /// <summary>
    /// Refills points to give on each company's reset day (#19). It runs inside the API, so it's deployed wherever the
    /// API is (#36; the separate Worker app never ran in Docker). Every instance may run it, because ResetDuePointsCommand
    /// claims each company's period in the database. Checks at startup, then every few minutes, so a refill comes
    /// shortly after midnight UTC, and one missed while the API was down comes as soon as it's back.
    /// Off when PointsReset:Enabled is false (the integration tests turn it off and send the command themselves).
    /// </summary>
    public sealed class PointsResetService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<PointsResetService> logger)
        : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!configuration.GetValue("PointsReset:Enabled", true))
            {
                logger.LogInformation("The points reset is off (PointsReset:Enabled is false)");
                return;
            }

            using var timer = new PeriodicTimer(Interval);
            try
            {
                do
                {
                    await RunOnceAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The API is shutting down
            }
        }

        private async Task RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Handlers are scoped (they use a DbContext), so each run gets its own scope
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ResetDuePointsCommand(DateTime.UtcNow), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Nothing is lost: whatever didn't commit is retried on the next run
                logger.LogError(ex, "The points reset failed; it runs again in {Interval}", Interval);
            }
        }
    }
}
