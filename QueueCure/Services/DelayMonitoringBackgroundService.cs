using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace QueueCure.Services
{
    public class DelayMonitoringBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DelayMonitoringBackgroundService> _logger;

        public DelayMonitoringBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<DelayMonitoringBackgroundService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Smart Delay Monitoring Background Service is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var delayDetectionService = scope.ServiceProvider.GetRequiredService<IDelayDetectionService>();
                        await delayDetectionService.CheckActiveConsultationsAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred during consultation delay checks.");
                }

                // Poll every 10 seconds
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }

            _logger.LogInformation("Smart Delay Monitoring Background Service is stopping.");
        }
    }
}
