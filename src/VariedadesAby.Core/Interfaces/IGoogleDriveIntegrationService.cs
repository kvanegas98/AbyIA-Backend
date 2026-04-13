using VariedadesAby.Core.DTOs.FtpDrive;

namespace VariedadesAby.Core.Interfaces;

public interface IGoogleDriveIntegrationService
{
    /// <summary>
    /// Sube un archivo desde un MemoryStream a la carpeta configurada en Google Drive.
    /// Si el archivo ya existe con el mismo nombre en la misma carpeta, no lo sube de nuevo.
    /// Retorna (DriveFileId, AlreadyExisted).
    /// </summary>
    Task<(string DriveFileId, bool AlreadyExisted)> UploadFileAsync(MemoryStream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Consulta Google Drive para obtener el estado del último backup:
    /// si Drive está autorizado, el archivo esperado hoy y el último .bak subido.
    /// </summary>
    Task<EstadoBackupDto> ObtenerEstadoBackupAsync(CancellationToken ct = default);
}
