using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.DTOs.Reporte;

namespace VariedadesAby.Core.Interfaces;

public interface IEmailNotificationService
{
    /// <summary>
    /// Envía correo de éxito. Retorna null si se envió correctamente,
    /// o el mensaje de error si falló (sin lanzar excepción).
    /// </summary>
    Task<string?> SendTransferSuccessAsync(TransferResult result, CancellationToken ct = default);

    /// <summary>
    /// Envía correo de fallo. Retorna null si se envió correctamente,
    /// o el mensaje de error si falló (sin lanzar excepción).
    /// </summary>
    Task<string?> SendTransferFailureAsync(string errorMessage, CancellationToken ct = default);

    /// <summary>Envía un correo de prueba para verificar la configuración SMTP.</summary>
    Task<string?> SendTestEmailAsync(CancellationToken ct = default);

    /// <summary>Envía el reporte diario de operaciones.</summary>
    Task<string?> SendReporteDiarioAsync(ReporteDiarioDto reporte, CancellationToken ct = default);
}
