using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SucursalController : ControllerBase
{
    private readonly ISucursalService _service;

    public SucursalController(ISucursalService service)
    {
        _service = service;
    }

    /// <summary>
    /// Comparativo de rendimiento entre sucursales.
    /// El período anterior se calcula automáticamente con la misma duración,
    /// inmediatamente antes de fechaDesde.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> Rendimiento(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        if (fechaDesde > fechaHasta)
            return BadRequest(new { mensaje = "fechaDesde no puede ser mayor que fechaHasta." });

        var resultado = await _service.GetRendimientoAsync(fechaDesde, fechaHasta);
        return Ok(resultado);
    }

    /// <summary>
    /// Ventas diarias por sucursal para gráfico de líneas.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> Tendencia(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta)
    {
        if (fechaDesde > fechaHasta)
            return BadRequest(new { mensaje = "fechaDesde no puede ser mayor que fechaHasta." });

        var resultado = await _service.GetTendenciaAsync(fechaDesde, fechaHasta);
        return Ok(resultado);
    }

    /// <summary>
    /// Top 10 artículos con mayor capital inmovilizado en stock.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> TopCapitalInmovilizado()
    {
        var resultado = await _service.GetTopCapitalInmovilizadoAsync();
        return Ok(resultado);
    }

    /// <summary>
    /// Inventario valorizado actual agrupado por categoría.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> InventarioCategoria()
    {
        var resultado = await _service.GetInventarioCategoriaAsync();
        return Ok(resultado);
    }

    /// <summary>
    /// Inventario valorizado actual: stock, costo y precio de venta por sucursal.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> InventarioValorizado()
    {
        var resultado = await _service.GetInventarioValorizadoAsync();
        return Ok(resultado);
    }

    /// <summary>
    /// Top N productos más vendidos por sucursal en el rango de fechas.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> TopProductos(
        [FromQuery] DateTime fechaDesde,
        [FromQuery] DateTime fechaHasta,
        [FromQuery] int top = 5)
    {
        if (fechaDesde > fechaHasta)
            return BadRequest(new { mensaje = "fechaDesde no puede ser mayor que fechaHasta." });

        var resultado = await _service.GetTopProductosAsync(fechaDesde, fechaHasta, top);
        return Ok(resultado);
    }
}
