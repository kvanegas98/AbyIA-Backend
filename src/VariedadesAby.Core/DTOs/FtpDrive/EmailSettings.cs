namespace VariedadesAby.Core.DTOs.FtpDrive;

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;

    /// <summary>True = SslOnConnect (puerto 465). False = StartTls (puerto 587).</summary>
    public bool UseSsl { get; set; } = false;

    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Variedades Aby Admin";

    /// <summary>Destinatarios del reporte diario de operaciones.</summary>
    public List<string> ReportRecipients { get; set; } = new();

    /// <summary>Destinatarios de notificaciones de backup (FTP → Drive).</summary>
    public List<string> BackupRecipients { get; set; } = new();
}
