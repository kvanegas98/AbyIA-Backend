namespace VariedadesAby.Core.DTOs.Reporte;

public class ReporteSettings
{
    /// <summary>
    /// Hora local en que se envía el reporte diario.
    /// Formato: "HH:mm:ss" — Ej: "18:30:00" = 6:30 PM.
    /// </summary>
    public TimeSpan HoraEnvioLocal { get; set; } = new TimeSpan(18, 30, 0);

    /// <summary>
    /// ID de zona horaria. Windows: "Central America Standard Time". Linux: "America/Managua".
    /// </summary>
    public string TimeZoneId { get; set; } = "Central America Standard Time";
}
