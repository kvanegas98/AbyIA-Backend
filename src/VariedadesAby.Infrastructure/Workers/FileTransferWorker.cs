using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Infrastructure.Helpers;

namespace VariedadesAby.Infrastructure.Workers;

/// <summary>
/// BackgroundService que ejecuta la transferencia FTP → Drive a la hora configurada cada día.
/// Usa IServiceScopeFactory para resolver los servicios Scoped de forma segura desde un Singleton.
/// </summary>
public sealed class FileTransferWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SchedulerSettings _schedulerSettings;
    private readonly ILogger<FileTransferWorker> _logger;

    public FileTransferWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SchedulerSettings> schedulerSettings,
        ILogger<FileTransferWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _schedulerSettings = schedulerSettings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[Worker] FileTransferWorker activo. Ejecución diaria programada a las {Time} hora Nicaragua.",
            _schedulerSettings.ScheduledTimeLocal);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNextRun();

            _logger.LogInformation(
                "[Worker] Próxima ejecución automática en {Hours:F1} h ({NextRun:yyyy-MM-dd HH:mm} UTC).",
                delay.TotalHours,
                DateTime.UtcNow.Add(delay));

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break; // Apagado limpio del host
            }

            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("[Worker] Disparando ejecución programada...");

            // Crear scope para resolver servicios Scoped (IFileTransferOrchestrator y sus dependencias)
            await using var scope = _scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IFileTransferOrchestrator>();

            await orchestrator.ExecuteTransferAsync(stoppingToken);
        }

        _logger.LogInformation("[Worker] FileTransferWorker detenido.");
    }

    /// <summary>
    /// Calcula el tiempo que falta para la próxima ejecución en base a la hora UTC configurada.
    /// Si la hora configurada ya pasó hoy, retorna el tiempo hasta la misma hora mañana.
    /// </summary>
    private TimeSpan CalculateDelayUntilNextRun()
    {
        var tz       = TimeZoneHelper.Resolve(_schedulerSettings.TimeZoneId);
        var localNow = TimeZoneHelper.NowIn(_schedulerSettings.TimeZoneId);
        var nextRun  = localNow.Date.Add(_schedulerSettings.ScheduledTimeLocal);

        if (nextRun <= localNow)
            nextRun = nextRun.AddDays(1);

        var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRun, tz);
        return nextRunUtc - DateTime.UtcNow;
    }
}
