using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VariedadesAby.Core.DTOs.Reporte;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Infrastructure.Helpers;

namespace VariedadesAby.Infrastructure.Workers;

/// <summary>
/// BackgroundService que envía el reporte diario de operaciones a las 6:30 PM hora Nicaragua.
/// </summary>
public sealed class ReporteDiarioWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReporteSettings _settings;
    private readonly ILogger<ReporteDiarioWorker> _logger;

    public ReporteDiarioWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReporteSettings> settings,
        ILogger<ReporteDiarioWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _settings     = settings.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[ReporteWorker] Activo. Envío diario programado a las {Hora} hora Nicaragua.",
            _settings.HoraEnvioLocal);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalcularDelayHastaProximoEnvio();

            _logger.LogInformation(
                "[ReporteWorker] Próximo envío en {Horas:F1} h ({ProximaVez:yyyy-MM-dd HH:mm} UTC).",
                delay.TotalHours,
                DateTime.UtcNow.Add(delay));

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (stoppingToken.IsCancellationRequested) break;

            _logger.LogInformation("[ReporteWorker] Ejecutando envío programado...");

            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<IReporteDiarioService>();

            try
            {
                var resultado = await service.GenerarYEnviarAsync(stoppingToken);
                _logger.LogInformation(
                    "[ReporteWorker] {Mensaje} (Ventas={Total}, Monto=C${Monto:N2})",
                    resultado.Mensaje, resultado.TotalVentas, resultado.TotalGlobal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReporteWorker] Error inesperado al generar/enviar el reporte.");
            }
        }

        _logger.LogInformation("[ReporteWorker] Detenido.");
    }

    private TimeSpan CalcularDelayHastaProximoEnvio()
    {
        var tz        = TimeZoneHelper.Resolve(_settings.TimeZoneId);
        var ahoraLocal = TimeZoneHelper.NowIn(_settings.TimeZoneId);
        var proximoEnvio = ahoraLocal.Date.Add(_settings.HoraEnvioLocal);

        if (proximoEnvio <= ahoraLocal)
            proximoEnvio = proximoEnvio.AddDays(1);

        var proximoEnvioUtc = TimeZoneInfo.ConvertTimeToUtc(proximoEnvio, tz);
        return proximoEnvioUtc - DateTime.UtcNow;
    }
}
