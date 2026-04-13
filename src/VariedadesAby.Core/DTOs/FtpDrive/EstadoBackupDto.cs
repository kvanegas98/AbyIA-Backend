namespace VariedadesAby.Core.DTOs.FtpDrive;

public class EstadoBackupDto
{
    /// <summary>Google Drive está autorizado y se pudo consultar.</summary>
    public bool DriveAutorizado { get; set; }

    /// <summary>Nombre del archivo esperado para el día de hoy (hora Nicaragua).</summary>
    public string ArchivoHoy { get; set; } = string.Empty;

    /// <summary>Si el archivo de hoy ya fue subido a Google Drive.</summary>
    public bool YaExisteHoy { get; set; }

    /// <summary>Nombre del último archivo .bak encontrado en Drive (puede ser de otro día).</summary>
    public string? UltimoArchivo { get; set; }

    /// <summary>ID de Google Drive del último archivo encontrado.</summary>
    public string? DriveFileId { get; set; }

    /// <summary>Fecha/hora UTC en que el último archivo fue creado en Drive.</summary>
    public DateTime? FechaSubidaUtc { get; set; }

    /// <summary>Fecha/hora local Nicaragua en que el último archivo fue creado en Drive.</summary>
    public DateTime? FechaSubidaLocal { get; set; }
}
