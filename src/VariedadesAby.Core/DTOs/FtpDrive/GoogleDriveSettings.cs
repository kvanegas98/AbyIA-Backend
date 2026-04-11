namespace VariedadesAby.Core.DTOs.FtpDrive;

public class GoogleDriveSettings
{
    /// <summary>
    /// Ruta al JSON de credenciales OAuth2 (tipo "Aplicación web") descargado de Google Cloud Console.
    /// </summary>
    public string OAuthClientSecretsPath { get; set; } = "credentials/oauth-client.json";

    /// <summary>
    /// Carpeta local donde se almacena el refresh token tras la autorización inicial.
    /// </summary>
    public string TokenStoragePath { get; set; } = "credentials/tokens";

    /// <summary>
    /// URI de redirección registrada en Google Cloud Console.
    /// Debe coincidir exactamente con la configurada en las credenciales OAuth2.
    /// </summary>
    public string OAuthRedirectUri { get; set; } = "http://localhost:5000/api/FtpDrive/auth/callback";

    /// <summary>ID de la carpeta en Google Drive donde se subirán los archivos.</summary>
    public string TargetFolderId { get; set; } = string.Empty;

    public string ApplicationName { get; set; } = "VariedadesAby FTP to Drive";
}
