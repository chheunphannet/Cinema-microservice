using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Reservation.Api.Repositories;

namespace Reservation.Api.Services;

public class ReservationCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReservationCleanupService> _logger;

    public ReservationCleanupService(IServiceProvider serviceProvider, ILogger<ReservationCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IReservationRepository>();
                
                var expiredCount = await repository.CleanupExpiredHoldsAsync();
                
                if (expiredCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} expired reservations in PostgreSQL", expiredCount);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning("Background stale reservation cleanup error: {Message}", ex.Message);
            }
        }
    }
}
