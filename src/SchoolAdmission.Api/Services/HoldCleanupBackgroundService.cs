using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class HoldCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HoldCleanupBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(5);

    public HoldCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<HoldCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HoldCleanupBackgroundService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepExpiredHoldsAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during expired holds sweep.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("HoldCleanupBackgroundService stopped.");
    }

    private async Task SweepExpiredHoldsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var utcNow = DateTime.UtcNow;

        var expiredHolds = await dbContext.Holds
            .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= utcNow)
            .ToListAsync(stoppingToken);

        if (expiredHolds.Count > 0)
        {
            foreach (var hold in expiredHolds)
            {
                hold.Status = HoldStatus.Expired;
            }

            await dbContext.SaveChangesAsync(stoppingToken);
            _logger.LogInformation("Swept {Count} expired holds to status EXPIRED.", expiredHolds.Count);
        }
    }
}

