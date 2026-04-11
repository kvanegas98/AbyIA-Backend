namespace VariedadesAby.Core.Interfaces;

public interface IFtpIntegrationService
{
    /// <summary>
    /// Se conecta al servidor FTP con Explicit TLS, descarga el archivo configurado
    /// en memoria y retorna el stream listo para leer + el nombre del archivo.
    /// No escribe nada en disco.
    /// </summary>
    Task<(MemoryStream Stream, string FileName)> DownloadFileAsync(CancellationToken ct = default);
}
