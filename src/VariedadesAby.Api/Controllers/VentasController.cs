using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.DTOs.Ventas;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VentasController : ControllerBase
{
    private readonly IVentasService _service;

    public VentasController(IVentasService service)
    {
        _service = service;
    }

    // PUT: api/Ventas/Anular/5/1
    [HttpPut("[action]/{id}/{sucursalId}")]
    public async Task<IActionResult> Anular(
        [FromRoute] int id,
        [FromRoute] int sucursalId,
        [FromBody] AnularVentaRequestDto dto)
    {
        if (id <= 0 || sucursalId <= 0)
            return BadRequest(new { mensaje = "Parámetros inválidos." });

        if (string.IsNullOrWhiteSpace(dto.Motivo))
            return BadRequest(new { mensaje = "El motivo de anulación es requerido." });

        if (dto.IdUsuario <= 0)
            return BadRequest(new { mensaje = "El usuario que anula es requerido." });

        try
        {
            await _service.AnularAsync(id, sucursalId, dto);
            return Ok(new { mensaje = "Venta anulada correctamente." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    // GET: api/Ventas/Buscar?codigoFactura=F-001&cliente=Juan&pagina=1&tamanoPagina=20
    [HttpGet("[action]")]
    public async Task<IActionResult> Buscar([FromQuery] BuscarVentasQueryDto filtro)
    {
        var resultado = await _service.BuscarAsync(filtro);
        return Ok(resultado);
    }

    // GET: api/Ventas/5/Detalle
    [HttpGet("{id}/[action]")]
    public async Task<IActionResult> Detalle([FromRoute] int id)
    {
        if (id <= 0)
            return BadRequest(new { mensaje = "ID de venta inválido." });

        try
        {
            var resultado = await _service.ObtenerDetalleAsync(id);
            return Ok(resultado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
    }

    // GET: api/Ventas/Clientes/Buscar?termino=Carmen
    [HttpGet("Clientes/Buscar")]
    public async Task<IActionResult> BuscarCliente([FromQuery] string termino)
    {
        if (string.IsNullOrWhiteSpace(termino) || termino.Length < 2)
            return BadRequest(new { mensaje = "El término debe tener al menos 2 caracteres." });

        var resultado = await _service.BuscarClientesAsync(termino);
        return Ok(resultado);
    }

    // GET: api/Ventas/Articulos/Buscar?termino=EV4155
    [HttpGet("Articulos/[action]")]
    public async Task<IActionResult> Buscar([FromQuery] string termino)
    {
        if (string.IsNullOrWhiteSpace(termino) || termino.Length < 2)
            return BadRequest(new { mensaje = "El término de búsqueda debe tener al menos 2 caracteres." });

        var resultado = await _service.BuscarArticulosAsync(termino);
        return Ok(resultado);
    }

    // GET: api/Ventas/FacturasPorArticulos?idsArticulos=48256&idsArticulos=12345&idCliente=13709
    [HttpGet("[action]")]
    public async Task<IActionResult> FacturasPorArticulos(
        [FromQuery] int[] idsArticulos,
        [FromQuery] int idCliente)
    {
        if (idsArticulos.Length == 0)
            return BadRequest(new { mensaje = "Debe indicar al menos un artículo." });

        if (idCliente <= 0)
            return BadRequest(new { mensaje = "El ID de cliente es requerido." });

        var resultado = await _service.BuscarFacturasPorArticulosAsync(idsArticulos, idCliente);
        return Ok(resultado);
    }
}
