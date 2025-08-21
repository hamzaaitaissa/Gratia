using Gratia.Application.Interfaces;

namespace Gratia.Worker
{
    public class PointResetWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<PointResetWorker> _logger;
        private DateTime _lastUpdate;

        public PointResetWorker( ILogger<PointResetWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (DateTime.UtcNow.Date.Day == 21 && _lastUpdate!=DateTime.UtcNow.Date)
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var pointResetService = scope.ServiceProvider.GetRequiredService<IPointResetService>();
                            await pointResetService.ResetPointMonthly();
                        }
                        _lastUpdate = DateTime.UtcNow.Date;
                    }
                }catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running monthly reset");
                }
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }
}
