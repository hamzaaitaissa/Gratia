using Gratia.Application.Interfaces;

namespace Gratia.Worker
{
    public class PointResetWorker : BackgroundService
    {
        private readonly IPointResetService _pointResetService;
        private readonly ILogger<PointResetWorker> _logger;
        private DateTime _lastUpdate;

        public PointResetWorker(IPointResetService pointResetService, ILogger<PointResetWorker> logger)
        {
            _pointResetService = pointResetService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (DateTime.UtcNow.Date.Day == 1 && DateTime.UtcNow.Hour == 0 && _lastUpdate!=DateTime.UtcNow.Date)
                    {
                        await _pointResetService.ResetPointMonthly();
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
