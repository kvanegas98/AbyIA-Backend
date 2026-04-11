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
        var (remotePath, fileName) = BuildTodayFilePath();

        _logger.LogInformation("[FTP] Descargando '{File}' desde {Host}:{Port}", fileName, _settings.Host, _settings.Port);

        var client = new AsyncFtpClient(_settings.Host, _settings.Username, _settings.Password, _settings.Port);

        // Explicit FTP over TLS en puerto 21 — requerido por SmarterASP
        client.Config.EncryptionMode = FtpEncryptionMode.Explicit;
        client.Config.ValidateAnyCertificate = !_settings.ValidateCertificate;

        try
        {
            await client.Connect(ct);
            _logger.LogInformation("[FTP] Conexión Explicit TLS establecida con {Host}", _settings.Host);

            var ms = new MemoryStream();
            var downloaded = await client.DownloadStream(ms, remotePath, token: ct);

            if (!downloaded)
                throw new FtpException($"El servidor FTP no encontró el archivo '{remotePath}'. Verifica que el backup del día ya haya sido generado.");

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
            _logger.LogError(ex, "[FTP] Error de protocolo al descargar '{File}'.", fileName);
            throw;
        }
        finally
        {
            await client.Disconnect(ct);
            client.Dispose();
        }
    }

    /// <summary>
    /// Construye la ruta remota y el nombre del archivo para el día actual usando
    /// los placeholders configurados en <see cref="FtpSettings.FileNamePattern"/>.
    /// Ej: /db/db_ab0a55_variedadesaby/CustomBackup_db_ab0a55_variedadesaby_4_7_2026_1.bak
    /// </summary>
    private (string RemotePath, string FileName) BuildTodayFilePath()
    {
        var today = TimeZoneHelper.NowIn(_schedulerSettings.TimeZoneId);

        var fileName = _settings.FileNamePattern
            .Replace("{M}",    today.Month.ToString())
            .Replace("{d}",    today.Day.ToString())
            .Replace("{yyyy}", today.Year.ToString());

        var remotePath = $"{_settings.RemoteDirectory.TrimEnd('/')}/{fileName}";

        _logger.LogDebug("[FTP] Ruta construida para hoy ({Date}): {Path}", today.ToString("yyyy-MM-dd"), remotePath);

        return (remotePath, fileName);
    }
}
