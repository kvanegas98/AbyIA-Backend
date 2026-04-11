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

    /// <summary>Lista de destinatarios que recibirán la notificación de transferencia.</summary>
    public List<string> ReportRecipients { get; set; } = new();
}
