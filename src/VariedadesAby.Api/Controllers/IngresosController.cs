using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.DTOs.Ingresos;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class IngresosController : ControllerBase
{
    private readonly IIngresosService _service;

    public IngresosController(IIngresosService service)
    {
        _service = service;
    }

    [HttpPost("[action]")]
    public async Task<IActionResult> Crear([FromBody] CrearIngresoViewModel model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var (idIngreso, numComprobante) = await _service.CrearAsync(model);
        return Ok(new { idIngreso, codigoFactura = numComprobante });
    }

    [HttpGet("[action]")]
    public async Task<IActionResult> Listar([FromQuery] FiltroIngresosViewModel filtro)
    {
        var resultado = await _service.ListarAsync(filtro);
        return Ok(resultado);
    }

    [HttpGet("[action]")]
    public async Task<IActionResult> AnalisisInventarioProveedor([FromQuery] FiltroAnalisisProveedorViewModel filtro)
    {
        var resultado = await _service.AnalisisInventarioPorProveedorAsync(filtro);
        return Ok(resultado);
    }

    [HttpGet("[action]/{idProveedor:int}")]
    public async Task<IActionResult> AnalisisDetalleProveedor(int idProveedor, [FromQuery] FiltroAnalisisProveedorViewModel filtro)
    {
        var resultado = await _service.AnalisisDetalleProveedorAsync(idProveedor, filtro);
        return Ok(resultado);
    }

    [HttpGet("[action]/{idIngreso:int}")]
    public async Task<IActionResult> Detalle(int idIngreso)
    {
        var detalle = await _service.ObtenerDetalleAsync(idIngreso);
        return Ok(detalle);
    }

    [HttpPost("[action]/{idIngreso:int}")]
    public async Task<IActionResult> Anular(int idIngreso)
    {
        try
        {
            var exito = await _service.AnularAsync(idIngreso);
            return Ok(new { success = exito, message = "Compra anulada correctamente." });
        }
        catch (VariedadesAby.Core.Exceptions.IngresoException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("[action]/{idIngreso:int}")]
    public async Task<IActionResult> Pdf(int idIngreso)
    {
        var pdf = await _service.GenerarPdfAsync(idIngreso);
        return File(pdf, "application/pdf", $"compra_{idIngreso}.pdf");
    }
}
