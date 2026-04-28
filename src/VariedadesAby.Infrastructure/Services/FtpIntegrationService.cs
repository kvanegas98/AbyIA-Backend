using FluentFTP;
using FluentFTP.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Infrastructure.Helpers;

namespace VariedadesAby.Infrastructure.Services;

public sealed class FtpIntegrationService : IFtpIntegrationService
{
    private readonly FtpSettings _settings;
    private readonly SchedulerSettings _schedulerSettings;
    private readonly ILogger<FtpIntegrationService> _logger;

    public FtpIntegrationService(
        IOptions<FtpSettings> settings,
        IOptions<SchedulerSettings> schedulerSettings,
        ILogger<FtpIntegrationService> logger)
    {
        _settings = settings.Value;
        _schedulerSettings = schedulerSettings.Value;
        _logger = logger;
    }

    public async Task<(MemoryStream Stream, string FileName)> DownloadFileAsync(CancellationToken ct = default)
    {
        var today = TimeZoneHelper.NowIn(_schedulerSettings.TimeZoneId);
        var prefix = BuildTodayPrefix(today);

        _logger.LogInformation("[FTP] Buscando archivos con prefijo '{Prefix}' en {Host}:{Port}", prefix, _settings.Host, _settings.Port);

        var client = new AsyncFtpClient(_settings.Host, _settings.Username, _settings.Password, _settings.Port);

        // Explicit FTP over TLS en puerto 21 — requerido por SmarterASP
        client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
        client.Config.ValidateAnyCertificate = !_settings.ValidateCertificate;

        try
        {
            await client.Connect(ct);
            _logger.LogInformation("[FTP] Conexión Explicit TLS establecida con {Host}", _settings.Host);

            var remoteDir = _settings.RemoteDirectory.TrimEnd('/');
            var listing = await client.GetListing(remoteDir, token: ct);

            // Tomar el archivo .bak del día con el número de sufijo más alto (_1, _2, _3…)
            var fileName = listing
                .Where(f => f.Type == FluentFTP.FtpObjectType.File
                         && f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                         && f.Name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                .Select(f => f.Name)
                .OrderByDescending(n => n)
                .FirstOrDefault()
                ?? throw new FtpException(
                    $"No se encontró ningún archivo que empiece con '{prefix}' en '{remoteDir}'. " +
                    "Verifica que el backup del día ya haya sido generado.");

            var remotePath = $"{remoteDir}/{fileName}";
            _logger.LogInformation("[FTP] Archivo seleccionado: '{File}'", fileName);

            var ms = new MemoryStream();
            var downloaded = await client.DownloadStream(ms, remotePath, token: ct);

            if (!downloaded)
                throw new FtpException($"El servidor FTP no pudo descargar '{remotePath}'.");

            ms.Position = 0;
            _logger.LogInformation("[FTP] '{File}' descargado en memoria. Tamaño: {Size:N0} bytes", fileName, ms.Length);

            return (ms, fileName);
        }
        catch (FtpAuthenticationException ex)
        {
            _logger.LogError(ex, "[FTP] Error 530 — Credenciales rechazadas por {Host}.", _settings.Host);
            throw;
        }
        catch (FtpException ex)
        {
            _logger.LogError(ex, "[FTP] Error de protocolo al descargar archivo del día.");
            throw;
        }
        finally
        {
            await client.Disconnect(ct);
            client.Dispose();
        }
    }

    private string BuildTodayPrefix(DateTime today) =>
        GoogleDriveIntegrationService.BuildDayPrefix(_settings.FileNamePattern, today);
}
