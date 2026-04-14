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
            "[Worker] FileTransferWorker activo. Ejecución diaria a las {Time} hora Nicaragua. " +
            "Reintentos configurados: {MaxRetries} (cada {Interval} min).",
            _schedulerSettings.ScheduledTimeLocal,
            _schedulerSettings.MaxRetries,
            _schedulerSettings.RetryIntervalMinutes);

        // Arranque tardío: si el Worker inicia después de la hora programada pero dentro
        // de la ventana de recuperación, verificar Drive y ejecutar si el backup no existe.
        await VerificarArranqueTardioAsync(stoppingToken);

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
            await EjecutarConReintentosAsync(stoppingToken);
        }

        _logger.LogInformation("[Worker] FileTransferWorker detenido.");
    }

    /// <summary>
    /// Al iniciar el Worker, detecta si el app pool fue reciclado después de la hora programada
    /// pero dentro de la ventana de recuperación. Si el backup de hoy no existe en Drive, ejecuta
    /// la transferencia de inmediato sin esperar al día siguiente.
    /// Ventana = (MaxRetries + 1) × RetryIntervalMinutes después de la hora programada.
    /// </summary>
    private async Task VerificarArranqueTardioAsync(CancellationToken ct)
    {
        var localNow      = TimeZoneHelper.NowIn(_schedulerSettings.TimeZoneId);
        var programadaHoy = localNow.Date.Add(_schedulerSettings.ScheduledTimeLocal);

        // Solo aplica si ya pasó la hora programada de hoy
        if (localNow <= programadaHoy) return;

        // Ventana: desde hora programada hasta (MaxRetries+1) × RetryIntervalMinutes después
        var ventanaMinutos = (_schedulerSettings.MaxRetries + 1) * _schedulerSettings.RetryIntervalMinutes;
        var finVentana     = programadaHoy.AddMinutes(ventanaMinutos);

        if (localNow > finVentana)
        {
            _logger.LogInformation(
                "[Worker] Arranque tardío fuera de ventana de recuperación ({ProgramadaHoy:HH:mm} – {FinVentana:HH:mm}). " +
                "Se esperará al día siguiente.",
                programadaHoy, finVentana);
            return;
        }

        _logger.LogWarning(
            "[Worker] Arranque tardío detectado ({LocalNow:HH:mm}). Ventana de recuperación hasta {FinVentana:HH:mm}. " +
            "Verificando si el backup de hoy ya existe en Drive...",
            localNow, finVentana);

        var yaExiste = await BackupDeHoyExisteEnDriveAsync(ct);
        if (yaExiste)
        {
            _logger.LogInformation("[Worker] Backup de hoy ya existe en Drive. No se requiere recuperación.");
            return;
        }

        _logger.LogWarning("[Worker] Backup de hoy NO encontrado en Drive. Ejecutando recuperación por arranque tardío...");
        await EjecutarConReintentosAsync(ct);
    }

    /// <summary>
    /// Ejecuta la transferencia y la reintenta hasta MaxRetries veces si falla,
    /// esperando RetryIntervalMinutes entre cada intento y verificando en Drive
    /// que el backup de hoy no exista antes de reintentar.
    /// </summary>
    private async Task EjecutarConReintentosAsync(CancellationToken ct)
    {
        var result = await EjecutarTransferenciaAsync(ct);

        if (result is null || result.IsSuccess)
            return;

        for (int intento = 1; intento <= _schedulerSettings.MaxRetries; intento++)
        {
            if (ct.IsCancellationRequested) break;

            _logger.LogWarning(
                "[Worker] Transferencia fallida. Reintento {Intento}/{Max} en {Min} minutos.",
                intento, _schedulerSettings.MaxRetries, _schedulerSettings.RetryIntervalMinutes);

            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(_schedulerSettings.RetryIntervalMinutes),
                    ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (ct.IsCancellationRequested) break;

            // Verificar en Drive antes de reintentar para no duplicar el backup
            var yaExiste = await BackupDeHoyExisteEnDriveAsync(ct);
            if (yaExiste)
            {
                _logger.LogInformation(
                    "[Worker] El backup de hoy ya existe en Google Drive. Se cancela el reintento {Intento}.",
                    intento);
                return;
            }

            _logger.LogInformation("[Worker] Disparando reintento {Intento}/{Max}...", intento, _schedulerSettings.MaxRetries);
            result = await EjecutarTransferenciaAsync(ct);

            if (result is not null && result.IsSuccess)
                return;
        }

        _logger.LogError("[Worker] Se agotaron los {Max} reintentos. La transferencia no pudo completarse hoy.", _schedulerSettings.MaxRetries);
    }

    private async Task<TransferResult?> EjecutarTransferenciaAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IFileTransferOrchestrator>();

        try
        {
            return await orchestrator.ExecuteTransferAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Worker] Error inesperado durante la transferencia.");
            return null;
        }
    }

    private async Task<bool> BackupDeHoyExisteEnDriveAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var driveService = scope.ServiceProvider.GetRequiredService<IGoogleDriveIntegrationService>();
            var estado = await driveService.ObtenerEstadoBackupAsync(ct);
            return estado.YaExisteHoy;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Worker] No se pudo verificar el estado del backup en Drive. Se procederá con el reintento.");
            return false; // Si no se puede verificar, reintentamos por precaución
        }
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
