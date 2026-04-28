using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Infrastructure.Helpers;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace VariedadesAby.Infrastructure.Services;

public sealed class GoogleDriveIntegrationService : IGoogleDriveIntegrationService
{
    private static readonly string[] MesesEspanol =
    [
        "Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
        "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"
    ];

    private readonly GoogleDriveSettings _settings;
    private readonly FtpSettings _ftpSettings;
    private readonly SchedulerSettings _schedulerSettings;
    private readonly ILogger<GoogleDriveIntegrationService> _logger;

    public GoogleDriveIntegrationService(
        IOptions<GoogleDriveSettings> settings,
        IOptions<FtpSettings> ftpSettings,
        IOptions<SchedulerSettings> schedulerSettings,
        ILogger<GoogleDriveIntegrationService> logger)
    {
        _settings = settings.Value;
        _ftpSettings = ftpSettings.Value;
        _schedulerSettings = schedulerSettings.Value;
        _logger = logger;
    }

    public async Task<(string DriveFileId, bool AlreadyExisted)> UploadFileAsync(MemoryStream stream, string fileName, CancellationToken ct = default)
    {
        _logger.LogInformation("[Drive] Iniciando subida de '{File}'", fileName);

        var credential = await GetUserCredentialAsync(ct);

        using var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _settings.ApplicationName
        });

        // Estructura dinámica: Raíz / 2026 / 04 - Abril / archivo.bak (hora local Nicaragua)
        var now = TimeZoneHelper.NowIn(_schedulerSettings.TimeZoneId);
        var yearFolderId  = await GetOrCreateFolderAsync(service, now.Year.ToString(), _settings.TargetFolderId, ct);
        var monthFolderId = await GetOrCreateFolderAsync(service, BuildMonthFolderName(now), yearFolderId, ct);

        // ── Verificar si el archivo ya existe en la carpeta del mes ──
        var existing = await FindExistingFileAsync(service, fileName, monthFolderId, ct);
        if (existing is not null)
        {
            _logger.LogWarning(
                "[Drive] El archivo '{File}' ya existe en Drive (ID: {Id}). Se omite la subida.",
                fileName, existing);
            return (existing, true);
        }

        var fileMetadata = new DriveFile
        {
            Name = fileName,
            Parents = [monthFolderId]
        };

        var request = service.Files.Create(fileMetadata, stream, ResolveMimeType(fileName));
        request.Fields = "id, name, size";

        var progress = await request.UploadAsync(ct);

        if (progress.Status == Google.Apis.Upload.UploadStatus.Failed)
        {
            throw new InvalidOperationException(
                $"Google Drive rechazó la subida de '{fileName}': {progress.Exception?.Message}",
                progress.Exception);
        }

        var uploaded = request.ResponseBody;
        _logger.LogInformation(
            "[Drive] '{File}' subido. Drive ID: {Id} | Tamaño: {Size:N0} bytes",
            uploaded.Name, uploaded.Id, uploaded.Size);

        return (uploaded.Id, false);
    }

    public async Task<EstadoBackupDto> ObtenerEstadoBackupAsync(CancellationToken ct = default)
    {
        var now = TimeZoneHelper.NowIn(_schedulerSettings.TimeZoneId);

        // Prefijo del día sin el número de sufijo (_1, _2…) para buscar en Drive
        var prefixHoy = BuildDayPrefix(_ftpSettings.FileNamePattern, now);

        // Nombre canónico para mostrar en el DTO (igual que antes, con _1)
        var archivoHoy = _ftpSettings.FileNamePattern
            .Replace("{M}",    now.Month.ToString())
            .Replace("{d}",    now.Day.ToString())
            .Replace("{yyyy}", now.Year.ToString());

        UserCredential credential;
        try
        {
            credential = await GetUserCredentialAsync(ct);
        }
        catch (UnauthorizedAccessException)
        {
            return new EstadoBackupDto { DriveAutorizado = false, ArchivoHoy = archivoHoy };
        }

        try
        {
            using var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = _settings.ApplicationName
            });

            // ── Último .bak subido (sin restricción de carpeta — in ancestors no existe en Drive API v3) ──
            var lastReq = service.Files.List();
            lastReq.Q = "mimeType != 'application/vnd.google-apps.folder' " +
                        "AND name contains '.bak' " +
                        "AND trashed=false";
            lastReq.Fields  = "files(id, name, createdTime)";
            lastReq.OrderBy = "createdTime desc";
            lastReq.PageSize = 1;

            var lastResult = await lastReq.ExecuteAsync(ct);
            var lastFile   = lastResult.Files.FirstOrDefault();

            // ── ¿Existe ya algún backup de hoy? (busca por prefijo para cubrir _1, _2…) ──
            var todayReq = service.Files.List();
            todayReq.Q = $"name contains '{prefixHoy}' AND name contains '.bak' AND trashed=false";
            todayReq.Fields   = "files(id)";
            todayReq.PageSize = 1;

            var todayResult = await todayReq.ExecuteAsync(ct);
            var yaExisteHoy = todayResult.Files.Count > 0;

            DateTime? fechaSubidaLocal = null;
            if (lastFile?.CreatedTimeDateTimeOffset is { } createdOffset)
            {
                var tz = TimeZoneHelper.Resolve(_schedulerSettings.TimeZoneId);
                fechaSubidaLocal = TimeZoneInfo.ConvertTimeFromUtc(createdOffset.UtcDateTime, tz);
            }

            return new EstadoBackupDto
            {
                DriveAutorizado  = true,
                ArchivoHoy       = archivoHoy,
                YaExisteHoy      = yaExisteHoy,
                UltimoArchivo    = lastFile?.Name,
                DriveFileId      = lastFile?.Id,
                FechaSubidaUtc   = lastFile?.CreatedTimeDateTimeOffset?.UtcDateTime,
                FechaSubidaLocal = fechaSubidaLocal
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "[Drive] Error al consultar estado del backup.");
            return new EstadoBackupDto { DriveAutorizado = false, ArchivoHoy = archivoHoy };
        }
    }

    /// <summary>
    /// Carga el refresh token guardado en disco y retorna un UserCredential listo para usar.
    /// Si no existe el token lanza una excepción indicando que hay que autorizar primero.
    /// </summary>
    public async Task<UserCredential> GetUserCredentialAsync(CancellationToken ct = default)
    {
        var flow = BuildFlow();
        var token = await flow.LoadTokenAsync("user", ct);

        if (token?.RefreshToken == null)
            throw new UnauthorizedAccessException(
                "Google Drive no está autorizado. Llama a GET /api/FtpDrive/auth/url, " +
                "abre el enlace en el navegador y autoriza el acceso con tu cuenta de Google.");

        return new UserCredential(flow, "user", token);
    }

    /// <summary>Construye la URL de consentimiento OAuth2 para mostrarla al administrador.</summary>
    public string BuildAuthorizationUrl()
    {
        // Cast necesario: CreateAuthorizationCodeRequest retorna el tipo base,
        // pero GoogleAuthorizationCodeFlow instancia GoogleAuthorizationCodeRequestUrl
        // que sí expone AccessType y Prompt.
        var request = (GoogleAuthorizationCodeRequestUrl)
            BuildFlow().CreateAuthorizationCodeRequest(_settings.OAuthRedirectUri);

        // offline → Google devuelve refresh_token (persistente)
        // consent → fuerza la pantalla de consentimiento aunque ya esté autorizado
        request.AccessType = "offline";
        request.Prompt = "consent";
        return request.Build().AbsoluteUri;
    }

    /// <summary>
    /// Intercambia el code recibido en el callback de OAuth2 por un access + refresh token
    /// y los guarda en disco para uso futuro.
    /// </summary>
    public async Task ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var flow = BuildFlow();
        await flow.ExchangeCodeForTokenAsync("user", code, _settings.OAuthRedirectUri, ct);
        _logger.LogInformation("[Drive] Token OAuth2 guardado exitosamente en '{Path}'.", _settings.TokenStoragePath);
    }

    // ── Helpers privados ────────────────────────────────────────────────────

    private GoogleAuthorizationCodeFlow BuildFlow() =>
        new(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = GoogleClientSecrets.FromFile(_settings.OAuthClientSecretsPath).Secrets,
            Scopes = [DriveService.ScopeConstants.Drive],
            DataStore = new FileDataStore(_settings.TokenStoragePath, fullPath: true)
        });

    /// <summary>
    /// Busca un archivo por nombre exacto dentro de <paramref name="parentId"/>.
    /// Retorna el ID si ya existe, o null si no hay duplicado.
    /// </summary>
    private async Task<string?> FindExistingFileAsync(
        DriveService service, string fileName, string parentId, CancellationToken ct)
    {
        var listRequest = service.Files.List();
        listRequest.Q = $"name='{fileName}' " +
                        $"AND '{parentId}' in parents " +
                        $"AND mimeType != 'application/vnd.google-apps.folder' " +
                        $"AND trashed=false";
        listRequest.Fields = "files(id, name)";
        listRequest.PageSize = 1;

        var result = await listRequest.ExecuteAsync(ct);
        return result.Files.Count > 0 ? result.Files[0].Id : null;
    }

    private async Task<string> GetOrCreateFolderAsync(
        DriveService service, string folderName, string parentId, CancellationToken ct)
    {
        var listRequest = service.Files.List();
        listRequest.Q = $"mimeType='application/vnd.google-apps.folder' " +
                        $"AND name='{folderName}' " +
                        $"AND '{parentId}' in parents " +
                        $"AND trashed=false";
        listRequest.Fields = "files(id, name)";
        listRequest.PageSize = 1;

        var result = await listRequest.ExecuteAsync(ct);

        if (result.Files.Count > 0)
        {
            _logger.LogDebug("[Drive] Carpeta existente: '{Name}' (ID: {Id})", folderName, result.Files[0].Id);
            return result.Files[0].Id;
        }

        var metadata = new DriveFile
        {
            Name = folderName,
            MimeType = "application/vnd.google-apps.folder",
            Parents = [parentId]
        };

        var createRequest = service.Files.Create(metadata);
        createRequest.Fields = "id, name";
        var folder = await createRequest.ExecuteAsync(ct);

        _logger.LogInformation("[Drive] Carpeta creada: '{Name}' (ID: {Id})", folder.Name, folder.Id);
        return folder.Id;
    }

    private static string BuildMonthFolderName(DateTime date) =>
        $"{date.Month:D2} - {MesesEspanol[date.Month - 1]}";

    /// <summary>
    /// Devuelve el prefijo del archivo del día sin el número de sufijo (_1, _2…).
    /// Ej: patrón "CustomBackup_{M}_{d}_{yyyy}_1.bak" → "CustomBackup_4_28_2026_"
    /// </summary>
    internal static string BuildDayPrefix(string pattern, DateTime date)
    {
        var withDate = pattern
            .Replace("{M}",    date.Month.ToString())
            .Replace("{d}",    date.Day.ToString())
            .Replace("{yyyy}", date.Year.ToString());

        var dotIdx = withDate.LastIndexOf('.');
        var nameNoExt = dotIdx >= 0 ? withDate[..dotIdx] : withDate;
        var lastUnderscore = nameNoExt.LastIndexOf('_');
        return lastUnderscore >= 0 ? nameNoExt[..(lastUnderscore + 1)] : nameNoExt + "_";
    }

    private static string ResolveMimeType(string fileName) =>
        Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".pdf"  => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls"  => "application/vnd.ms-excel",
            ".csv"  => "text/csv",
            ".zip"  => "application/zip",
            ".rar"  => "application/x-rar-compressed",
            ".txt"  => "text/plain",
            ".json" => "application/json",
            ".xml"  => "application/xml",
            ".bak"  => "application/octet-stream",
            _       => "application/octet-stream"
        };
}
