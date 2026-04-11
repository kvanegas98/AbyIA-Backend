namespace VariedadesAby.Core.DTOs.FtpDrive;

public class TransferResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? DriveFileId { get; set; }
    public bool EmailSent { get; set; }
    public string? EmailError { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;

    public bool AlreadyExisted { get; set; }

    public static TransferResult Success(string fileName, string driveFileId, bool alreadyExisted = false) => new()
    {
        IsSuccess = true,
        FileName = fileName,
        DriveFileId = driveFileId,
        AlreadyExisted = alreadyExisted,
        Message = alreadyExisted
            ? $"El archivo '{fileName}' ya existía en Google Drive (ID: {driveFileId}). No se subió de nuevo."
            : $"Archivo '{fileName}' transferido exitosamente a Google Drive (ID: {driveFileId}).",
        ExecutedAt = DateTime.UtcNow
    };

    public static TransferResult Failure(string error) => new()
    {
        IsSuccess = false,
        Message = error,
        ExecutedAt = DateTime.UtcNow
    };
}
