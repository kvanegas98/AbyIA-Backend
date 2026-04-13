using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ComprasPdfController : ControllerBase
{
    private readonly IComprasPdfService _service;

    public ComprasPdfController(IComprasPdfService service)
    {
        _service = service;
    }

    /// <summary>
    /// Recibe un PDF de factura de proveedor, lo convierte a imágenes (guardadas en Cloudinary),
    /// usa Gemini Vision para extraer los datos y valida los artículos contra la BD.
    /// Retorna un objeto listo para enviarse al endpoint Crear de compras.
    /// </summary>
    [HttpPost("[action]")]
    [RequestSizeLimit(10_485_760)] // 10 MB
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AnalizarPdf(
        [FromForm] IFormFile archivo,
        [FromForm] int idProveedor,
        [FromForm] int idUsuario,
        [FromForm] int idSucursal)
    {
        if (archivo is null || archivo.Length == 0)
            return BadRequest(new { mensaje = "Debe adjuntar un archivo PDF." });

        var esExtensionPdf = Path.GetExtension(archivo.FileName)
            .Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        var esContentTypePdf = archivo.ContentType
            .Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

        if (!esExtensionPdf && !esContentTypePdf)
            return BadRequest(new { mensaje = "Solo se aceptan archivos en formato PDF." });

        // Validar firma de bytes: un PDF válido siempre empieza con "%PDF-"
        var buffer = new byte[5];
        await archivo.OpenReadStream().ReadAsync(buffer, 0, 5);
        if (System.Text.Encoding.ASCII.GetString(buffer) != "%PDF-")
            return BadRequest(new { mensaje = "El archivo no es un PDF válido." });

        try
        {
            var resultado = await _service.AnalizarPdfAsync(archivo.OpenReadStream(), idProveedor, idUsuario, idSucursal);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Verifica si un artículo existe en la BD por su código.
    /// Retorna esNuevo=true si no se encuentra.
    /// </summary>
    // GET: api/ComprasPdf/ValidarArticulo?codigo=ABC123
    [HttpGet("[action]")]
    public async Task<IActionResult> ValidarArticulo([FromQuery] string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return BadRequest(new { mensaje = "El código no puede estar vacío." });

        var resultado = await _service.ValidarArticuloAsync(codigo);
        return Ok(resultado);
    }
}
