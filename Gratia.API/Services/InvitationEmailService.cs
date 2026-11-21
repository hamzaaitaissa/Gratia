using Gratia.Application.Features.Invitations.Commands;
using Gratia.Application.Interfaces;
using Mediator;

namespace Gratia.API.Services
{
    /// <summary>
    /// Wakes <see cref="InvitationEmailService"/> as soon as an invitation email is queued (#55). A singleton; several
    /// wake-ups before it runs count as one.
    /// </summary>
    public sealed class InvitationEmailSignal : IInvitationEmailQueue
    {
        private readonly SemaphoreSlim _signal = new(0, 1);

        public void Notify()
        {
            try
            {
                _signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Already woken
            }
        }

        // True when woken, false when the timeout came first
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => _signal.WaitAsync(timeout, cancellationToken);
    }

    /// <summary>
    /// Sends invitation emails in the background (#55): at once when one is queued here (<see cref="InvitationEmailSignal"/>),
    /// and every few seconds otherwise, which also picks up retries and emails queued by another instance. Every instance
    /// may run it, because SendInvitationEmailsCommand claims each email in the database first.
    /// Off when InvitationEmails:Enabled is false (the integration tests turn it off and send the command themselves).
    /// </summary>
    public sealed class InvitationEmailService(
        IServiceScopeFactory scopeFactory,
        InvitationEmailSignal signal,
        IConfiguration configuration,
        ILogger<InvitationEmailService> logger)
        : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!configuration.GetValue("InvitationEmails:Enabled", true))
            {
                logger.LogInformation("Invitation emails aren't sent by this instance (InvitationEmails:Enabled is false)");
                return;
            }

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // A full batch may mean more are waiting
                    while (await RunOnceAsync(stoppingToken) >= SendInvitationEmailsCommandHandler.BatchSize)
                    {
                    }

                    await signal.WaitAsync(Interval, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The API is shutting down; an email it had claimed is taken again once the claim runs out
            }
        }

        private async Task<int> RunOnceAsync(CancellationToken stoppingToken)
        {
            try
            {
                // Handlers are scoped (they use a DbContext), so each run gets its own scope
                using var scope = scopeFactory.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new SendInvitationEmailsCommand(DateTime.UtcNow), stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Nothing is lost: the emails stay queued, and the next run tries them
                logger.LogError(ex, "Sending invitation emails failed; trying again in {Interval}", Interval);
                return 0;
            }
        }
    }
}
