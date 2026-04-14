namespace VariedadesAby.Core.DTOs.FtpDrive;

public class SchedulerSettings
{
    /// <summary>
    /// Hora local diaria en que el Worker ejecuta la transferencia automática.
    /// Formato: "HH:mm:ss" — Ej: "02:00:00" = 2:00 AM hora local.
    /// </summary>
    public TimeSpan ScheduledTimeLocal { get; set; } = new TimeSpan(2, 0, 0);

    /// <summary>
    /// ID de zona horaria del sistema operativo.
    /// Windows: "Central America Standard Time" (Nicaragua, UTC-6).
    /// Linux/IANA: "America/Managua".
    /// </summary>
    public string TimeZoneId { get; set; } = "Central America Standard Time";

    /// <summary>
    /// Número máximo de reintentos automáticos si la transferencia falla.
    /// Default: 2 (reintenta a los 30 y 60 minutos de la hora programada).
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Minutos de espera entre cada reintento.
    /// Default: 30 minutos.
    /// </summary>
    public int RetryIntervalMinutes { get; set; } = 30;
}
