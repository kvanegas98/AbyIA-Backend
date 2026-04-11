namespace VariedadesAby.Core.DTOs.FtpDrive;

public class FtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 21;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Carpeta remota donde están los backups. Ej: /db/db_ab0a55_variedadesaby
    /// </summary>
    public string RemoteDirectory { get; set; } = string.Empty;

    /// <summary>
    /// Patrón del nombre de archivo con placeholders de fecha:
    ///   {M}    → mes sin cero (4)
    ///   {d}    → día sin cero (7)
    ///   {yyyy} → año completo (2026)
    /// Ej: "CustomBackup_db_ab0a55_variedadesaby_{M}_{d}_{yyyy}_1.bak"
    /// </summary>
    public string FileNamePattern { get; set; } = string.Empty;

    /// <summary>
    /// En producción establece true para validar el certificado TLS del servidor.
    /// En SmarterASP puede requerirse false si el cert es auto-firmado.
    /// </summary>
    public bool ValidateCertificate { get; set; } = false;
}
