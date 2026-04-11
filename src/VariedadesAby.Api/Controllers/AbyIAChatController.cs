using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.DTOs.Chat;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Core.Wrappers;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class AbyIAChatController : ControllerBase
{
    private readonly IAbyIAService _abyIAService;
    private readonly IHistorialChatRepository _historialRepo;
    private readonly IChatExportService _exportService;

    public AbyIAChatController(
        IAbyIAService abyIAService,
        IHistorialChatRepository historialRepo,
        IChatExportService exportService)
    {
        _abyIAService = abyIAService;
        _historialRepo = historialRepo;
        _exportService = exportService;
    }

    /// <summary>
    /// Enviar una pregunta al chat IA (Text-to-SQL)
    /// </summary>
    // POST: api/AbyIAChat/Preguntar
    [AllowAnonymous]
    [HttpPost("[action]")]
    public async Task<IActionResult> Preguntar([FromBody] ChatPreguntaDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
        {
            return BadRequest(ApiResponse<object>.Fail("La pregunta no puede estar vacía."));
        }

        if (request.Pregunta.Length > 500)
        {
            return BadRequest(ApiResponse<object>.Fail("La pregunta no puede exceder los 500 caracteres."));
        }

        var idUsuario = ObtenerIdUsuario();
        var respuesta = await _abyIAService.ProcesarPreguntaAsync(request.Pregunta, idUsuario);

        return Ok(ApiResponse<ChatRespuestaDto>.Ok(respuesta));
    }

    /// <summary>
    /// Exportar resultado de una pregunta a PDF
    /// </summary>
    // POST: api/AbyIAChat/ExportarPdf
    [AllowAnonymous]
    [HttpPost("[action]")]
    public async Task<IActionResult> ExportarPdf([FromBody] ChatPreguntaDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
        {
            return BadRequest(ApiResponse<object>.Fail("La pregunta no puede estar vacía."));
        }

        var idUsuario = ObtenerIdUsuario();
        var respuesta = await _abyIAService.ProcesarPreguntaAsync(request.Pregunta, idUsuario);

        if (!respuesta.Exito || respuesta.Datos == null)
        {
            return BadRequest(ApiResponse<object>.Fail(respuesta.Respuesta));
        }

        var datos = ConvertirDatos(respuesta.Datos);
        var pdfBytes = _exportService.ExportarPdf(respuesta.Pregunta, respuesta.Respuesta, datos);

        return File(pdfBytes, "application/pdf", $"ChatAbyIA_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
    }

    /// <summary>
    /// Exportar resultado de una pregunta a Excel
    /// </summary>
    // POST: api/AbyIAChat/ExportarExcel
    [AllowAnonymous]
    [HttpPost("[action]")]
    public async Task<IActionResult> ExportarExcel([FromBody] ChatPreguntaDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Pregunta))
        {
            return BadRequest(ApiResponse<object>.Fail("La pregunta no puede estar vacía."));
        }

        var idUsuario = ObtenerIdUsuario();
        var respuesta = await _abyIAService.ProcesarPreguntaAsync(request.Pregunta, idUsuario);

        if (!respuesta.Exito || respuesta.Datos == null)
        {
            return BadRequest(ApiResponse<object>.Fail(respuesta.Respuesta));
        }

        var datos = ConvertirDatos(respuesta.Datos);
        var excelBytes = _exportService.ExportarExcel(respuesta.Pregunta, datos);

        return File(excelBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"ChatAbyIA_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    /// <summary>
    /// Obtener historial de preguntas del usuario autenticado
    /// </summary>
    // GET: api/AbyIAChat/Historial?cantidad=20
    [HttpGet("[action]")]
    public async Task<IActionResult> Historial([FromQuery] int cantidad = 20)
    {
        if (cantidad < 1 || cantidad > 100)
        {
            cantidad = 20;
        }

        var idUsuario = ObtenerIdUsuario();
        var historial = await _historialRepo.ObtenerHistorialAsync(idUsuario, cantidad);

        return Ok(ApiResponse<object>.Ok(historial));
    }

    /// <summary>
    /// Obtiene el ID del usuario autenticado desde el token JWT.
    /// Retorna 0 si el usuario no está autenticado (endpoints AllowAnonymous).
    /// Lanza UnauthorizedAccessException si hay token pero el claim es inválido.
    /// </summary>
    private int ObtenerIdUsuario()
    {
        if (User.Identity?.IsAuthenticated != true)
            return 0; // Usuario anónimo — el historial no se guarda

        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("sub")
                    ?? User.FindFirst("idUsuario");

        if (claim != null && int.TryParse(claim.Value, out var id))
            return id;

        throw new UnauthorizedAccessException("No se pudo identificar al usuario del token.");
    }

    /// <summary>
    /// Convierte los datos dinámicos de Dapper a una lista de diccionarios
    /// </summary>
    private static List<IDictionary<string, object>> ConvertirDatos(object datos)
    {
        if (datos is IEnumerable<IDictionary<string, object>> lista)
        {
            return lista.ToList();
        }

        // Si los datos vienen como lista de objetos dinámicos
        if (datos is IEnumerable<dynamic> dinamicos)
        {
            return dinamicos
                .Select(d => (IDictionary<string, object>)d)
                .ToList();
        }

        return new List<IDictionary<string, object>>();
    }
}
