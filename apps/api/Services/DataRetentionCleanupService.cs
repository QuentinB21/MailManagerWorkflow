using MailManager.Api.Configuration;
using MailManager.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MailManager.Api.Services;

public sealed class DataRetentionCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<DataRetentionOptions> options,
    ILogger<DataRetentionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RemoveExpiredProcessingLogsAsync(stoppingToken);
        var interval = TimeSpan.FromHours(Math.Clamp(options.Value.CleanupIntervalHours, 1, 168));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RemoveExpiredProcessingLogsAsync(stoppingToken);
        }
    }

    private async Task RemoveExpiredProcessingLogsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retentionDays = Math.Clamp(options.Value.ProcessingLogsDays, 1, 3650);
            var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MailManagerDbContext>();
            var deleted = await dbContext.ProcessingLogs
                .Where(item => item.ProcessedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);
            if (deleted > 0)
            {
                logger.LogInformation("{Count} entrées d'historique expirées ont été supprimées.", deleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Arrêt normal de l'application.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "La purge de l'historique a échoué.");
        }
    }
}
