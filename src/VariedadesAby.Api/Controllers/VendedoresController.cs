using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.DTOs.Vendedores;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class VendedoresController : ControllerBase
{
    private readonly IVendedoresService _service;

    public VendedoresController(IVendedoresService service)
    {
        _service = service;
    }

    /// <summary>
    /// Panel ejecutivo de vendedores con desglose Contado / Crédito.
    /// Incluye KPIs globales + ranking individual por rango de fechas.
    /// Solo retorna vendedores con ventas > 0 en el rango.
    /// </summary>
    [HttpGet("[action]")]
    public async Task<IActionResult> Panel([FromQuery] FiltroVendedoresViewModel filtro)
    {
        if (filtro.FechaInicio > filtro.FechaFin)
            return BadRequest(new { message = "La fecha de inicio no puede ser mayor a la fecha fin." });

        var resultado = await _service.GetPanelAsync(filtro);
        return Ok(resultado);
    }
}
