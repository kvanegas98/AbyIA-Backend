using VariedadesAby.Core.DTOs.Reporte;

namespace VariedadesAby.Core.Interfaces;

public interface IReporteDiarioService
{
    /// <summary>
    /// Genera el reporte del día y lo envía por correo.
    /// Retorna Enviado=false si no hay ventas registradas (correo no se envía).
    /// </summary>
    Task<ReporteEnvioResultado> GenerarYEnviarAsync(CancellationToken ct = default);
}
