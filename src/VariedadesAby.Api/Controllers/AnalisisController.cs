using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.DTOs.Analisis;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AnalisisController : ControllerBase
{
    private readonly IAnalisisService _service;

    public AnalisisController(IAnalisisService service)
    {
        _service = service;
    }

    /// <summary>
    /// Clientes ordenados por gasto total con categoría VIP/Frecuente/Ocasional/Nuevo
    /// y semáforo según días sin comprar. Soporta búsqueda y paginación.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> ClientasOro([FromQuery] FiltroAnalisisViewModel filtro)
    {
        var resultado = await _service.GetClientasOroAsync(filtro);
        return Ok(resultado);
    }

    /// <summary>
    /// Productos con stock ordenados por velocidad de venta (últimos 30 días).
    /// Incluye días para agotarse y recomendación. Soporta búsqueda y paginación.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> VelocidadProductos([FromQuery] FiltroAnalisisViewModel filtro)
    {
        var resultado = await _service.GetVelocidadProductosAsync(filtro);
        return Ok(resultado);
    }

    /// <summary>
    /// Categorías con productos activos en stock, ordenadas por cantidad de productos.
    /// Usar para poblar el dropdown de filtro en Velocidad de Productos.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> CategoriasVelocidad()
    {
        var resultado = await _service.GetCategoriasVelocidadAsync();
        return Ok(resultado);
    }

    /// <summary>
    /// Proveedores ordenados por ROI real. Cruza precio compra vs precio venta real.
    /// Soporta búsqueda y paginación.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> RendimientoProveedores([FromQuery] FiltroAnalisisViewModel filtro)
    {
        var resultado = await _service.GetRendimientoProveedoresAsync(filtro);
        return Ok(resultado);
    }
}
