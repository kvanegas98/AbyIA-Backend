using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Infrastructure.Services;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class FtpDriveController : ControllerBase
{
    private readonly IFileTransferOrchestrator _orchestrator;
    private readonly GoogleDriveIntegrationService _driveService;
    private readonly ILogger<FtpDriveController> _logger;

    public FtpDriveController(
        IFileTransferOrchestrator orchestrator,
        IGoogleDriveIntegrationService driveService,
        ILogger<FtpDriveController> logger)
    {
        _orchestrator = orchestrator;
        _driveService = (GoogleDriveIntegrationService)driveService;
        _logger = logger;
    }

    // ── Transferencia ──────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta manualmente la transferencia FTP → Google Drive.
    /// Requiere haber autorizado Google Drive previamente via GET /auth/url.
    /// </summary>
    [HttpPost("[action]")]
    public async Task<IActionResult> Ejecutar(CancellationToken ct)
    {
        _logger.LogInformation("[FtpDriveController] Ejecución manual solicitada.");
        var result = await _orchestrator.ExecuteTransferAsync(ct);

        return result.IsSuccess
            ? Ok(result)
            : StatusCode(StatusCodes.Status500InternalServerError, result);
    }

    // ── Diagnóstico ────────────────────────────────────────────────────────

    /// <summary>
    /// Envía un correo de prueba para verificar que la configuración SMTP es correcta.
    /// Si falla, devuelve el error exacto de SMTP.
    /// </summary>
    [HttpPost("test-email")]
    public async Task<IActionResult> TestEmail(
        [FromServices] IEmailNotificationService emailService,
        CancellationToken ct)
    {
        var error = await emailService.SendTestEmailAsync(ct);

        return error is null
            ? Ok(new { success = true, message = "Correo de prueba enviado correctamente." })
            : BadRequest(new { success = false, smtpError = error });
    }

    // ── Autorización OAuth2 Google Drive (setup único) ─────────────────────

    /// <summary>
    /// Paso 1 — Devuelve la URL de consentimiento de Google.
    /// Abre esa URL en el navegador, inicia sesión con tu cuenta de Gmail
    /// y acepta los permisos. Google redirigirá automáticamente al callback.
    /// </summary>
    [HttpGet("auth/url")]
    public IActionResult ObtenerUrlAutorizacion()
    {
        var url = _driveService.BuildAuthorizationUrl();
        return Ok(new { url, instruccion = "Abre esta URL en el navegador para autorizar el acceso a Google Drive." });
    }

    /// <summary>
    /// Paso 2 (automático) — Google redirige aquí tras la autorización.
    /// Intercambia el código por un refresh token y lo guarda en disco.
    /// Después de esto el endpoint /Ejecutar ya funcionará.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("auth/callback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string code, [FromQuery] string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogError("[Drive Auth] El usuario rechazó el acceso o ocurrió un error: {Error}", error);
            return BadRequest(new { message = $"Autorización rechazada: {error}" });
        }

        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "No se recibió el código de autorización de Google." });

        await _driveService.ExchangeCodeAsync(code, ct);

        return Ok(new
        {
            message = "✅ Google Drive autorizado exitosamente. El token ha sido guardado.",
            proximoPaso = "Ya puedes usar POST /api/FtpDrive/Ejecutar para realizar la transferencia."
        });
    }
}
