using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Core.Wrappers;
using VariedadesAby.Core.DTOs.Dashboard;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen()
    {
        var resumen = await _dashboardService.GetResumenAsync();
        return Ok(ApiResponse<DashboardResumenDto>.Ok(resumen));
    }

    [HttpGet("finanzas")]
    public async Task<IActionResult> GetFinanzas([FromQuery] int dias = 30)
    {
        var finanzas = await _dashboardService.GetFinanzasAsync(dias);
        return Ok(ApiResponse<DashboardFinanzasDto>.Ok(finanzas));
    }

    [HttpGet("top-productos")]
    public async Task<IActionResult> GetTopProductos([FromQuery] int top = 5)
    {
        var productos = await _dashboardService.GetTopProductosAsync(top);
        return Ok(ApiResponse<IEnumerable<DashboardTopProductoDto>>.Ok(productos));
    }

    [HttpGet("transacciones-sospechosas")]
    public async Task<IActionResult> GetTransaccionesSospechosas([FromQuery] int dias = 7)
    {
        var transacciones = await _dashboardService.GetTransaccionesSospechosasAsync(dias);
        return Ok(ApiResponse<IEnumerable<DashboardTransaccionSospechosaDto>>.Ok(transacciones));
    }

    [HttpGet("finanzas-resumen")]
    public async Task<IActionResult> GetFinanzasResumen()
    {
        var resumen = await _dashboardService.GetFinanzasResumenAsync();
        return Ok(ApiResponse<DashboardFinanzasResumenDto>.Ok(resumen));
    }
}
