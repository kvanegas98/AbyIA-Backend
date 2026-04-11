using VariedadesAby.Core.DTOs.FtpDrive;

namespace VariedadesAby.Core.Interfaces;

public interface IFileTransferOrchestrator
{
    /// <summary>
    /// Orquesta el flujo completo: FTP Download → Drive Upload → Email Notification.
    /// Es el punto de entrada tanto para el endpoint manual como para el Worker programado.
    /// </summary>
    Task<TransferResult> ExecuteTransferAsync(CancellationToken ct = default);
}
