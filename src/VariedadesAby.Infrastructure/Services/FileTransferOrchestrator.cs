using Microsoft.Extensions.Logging;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public sealed class FileTransferOrchestrator : IFileTransferOrchestrator
{
    private readonly IFtpIntegrationService _ftpService;
    private readonly IGoogleDriveIntegrationService _driveService;
    private readonly IEmailNotificationService _emailService;
    private readonly ILogger<FileTransferOrchestrator> _logger;

    public FileTransferOrchestrator(
        IFtpIntegrationService ftpService,
        IGoogleDriveIntegrationService driveService,
        IEmailNotificationService emailService,
        ILogger<FileTransferOrchestrator> logger)
    {
        _ftpService = ftpService;
        _driveService = driveService;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<TransferResult> ExecuteTransferAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("[Orchestrator] ═══ Iniciando ciclo FTP → Google Drive ═══");

        TransferResult result;

        try
        {
            // ── Paso 1: Descargar desde FTP hacia MemoryStream ──
            var (stream, fileName) = await _ftpService.DownloadFileAsync(ct);

            await using (stream)
            {
                // ── Paso 2: Subir desde MemoryStream a Google Drive ──
                var (driveFileId, alreadyExisted) = await _driveService.UploadFileAsync(stream, fileName, ct);
                result = TransferResult.Success(fileName, driveFileId, alreadyExisted);
            }

            // ── Paso 3: Notificar éxito por correo ──
            var emailError = await _emailService.SendTransferSuccessAsync(result, ct);
            result.EmailSent  = emailError is null;
            result.EmailError = emailError;

            if (emailError is not null)
                _logger.LogError("[Orchestrator] La transferencia fue exitosa pero el correo falló: {Err}", emailError);
            else
                _logger.LogInformation("[Orchestrator] ═══ Transferencia y correo completados ═══");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[Orchestrator] Transferencia cancelada.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Orchestrator] Error crítico durante la transferencia.");
            result = TransferResult.Failure(ex.Message);

            // ── Notificar fallo por correo ──
            var emailError = await _emailService.SendTransferFailureAsync(ex.ToString(), ct);
            result.EmailSent  = emailError is null;
            result.EmailError = emailError;
        }

        return result;
    }
}
