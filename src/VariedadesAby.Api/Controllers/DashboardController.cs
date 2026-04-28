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

    // Resumen — métricas de hoy + utilidad del rango seleccionado
    // GET /api/Dashboard/resumen?fechaInicio=2026-03-18&fechaFin=2026-04-16
    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen(
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin    = null)
    {
        var resumen = await _dashboardService.GetResumenAsync(fechaInicio, fechaFin);
        return Ok(ApiResponse<DashboardResumenDto>.Ok(resumen));
    }

    // Ingresos vs Egresos por rango
    // GET /api/Dashboard/finanzas?fechaInicio=2025-03-01&fechaFin=2025-04-16
    [HttpGet("finanzas")]
    public async Task<IActionResult> GetFinanzas(
        [FromQuery] DateTime? fechaInicio,
        [FromQuery] DateTime? fechaFin)
    {
        var hasta = (fechaFin   ?? DateTime.Today).Date;
        var desde = (fechaInicio ?? hasta.AddDays(-29)).Date;

        var finanzas = await _dashboardService.GetFinanzasAsync(desde, hasta);
        return Ok(ApiResponse<DashboardFinanzasDto>.Ok(finanzas));
    }

    // Top productos por rango
    // GET /api/Dashboard/top-productos?top=5&fechaInicio=2025-03-01&fechaFin=2025-04-16
    [HttpGet("top-productos")]
    public async Task<IActionResult> GetTopProductos(
        [FromQuery] int       top          = 5,
        [FromQuery] DateTime? fechaInicio  = null,
        [FromQuery] DateTime? fechaFin     = null)
    {
        var hasta = (fechaFin   ?? DateTime.Today).Date;
        var desde = (fechaInicio ?? hasta.AddDays(-29)).Date;

        var productos = await _dashboardService.GetTopProductosAsync(top, desde, hasta);
        return Ok(ApiResponse<IEnumerable<DashboardTopProductoDto>>.Ok(productos));
    }

    // Top categorías por rango
    // GET /api/Dashboard/top-categorias?top=8&fechaInicio=2025-03-01&fechaFin=2025-04-16
    [HttpGet("top-categorias")]
    public async Task<IActionResult> GetTopCategorias(
        [FromQuery] int       top         = 8,
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin    = null)
    {
        var hasta = (fechaFin   ?? DateTime.Today).Date;
        var desde = (fechaInicio ?? hasta.AddDays(-29)).Date;

        var categorias = await _dashboardService.GetTopCategoriasAsync(top, desde, hasta);
        return Ok(ApiResponse<IEnumerable<DashboardTopCategoriaDto>>.Ok(categorias));
    }

    // Ventas por hora del día
    // GET /api/Dashboard/ventas-hora?fecha=2025-04-16
    [HttpGet("ventas-hora")]
    public async Task<IActionResult> GetVentasPorHora([FromQuery] DateTime? fecha)
    {
        var dia = (fecha ?? DateTime.Today).Date;
        var ventasHora = await _dashboardService.GetVentasPorHoraAsync(dia);
        return Ok(ApiResponse<IEnumerable<DashboardVentasPorHoraDto>>.Ok(ventasHora));
    }

    // Rendimiento por sucursal por rango
    // GET /api/Dashboard/sucursales?fechaInicio=2025-03-01&fechaFin=2025-04-16
    [HttpGet("sucursales")]
    public async Task<IActionResult> GetVentasPorSucursal(
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin    = null)
    {
        var hasta = (fechaFin   ?? DateTime.Today).Date;
        var desde = (fechaInicio ?? hasta.AddDays(-29)).Date;

        var sucursales = await _dashboardService.GetVentasPorSucursalAsync(desde, hasta);
        return Ok(ApiResponse<IEnumerable<DashboardSucursalResumenDto>>.Ok(sucursales));
    }

    // Aging de cartera — siempre al día de hoy
    // GET /api/Dashboard/aging-cartera
    [HttpGet("aging-cartera")]
    public async Task<IActionResult> GetAgingCartera()
    {
        var aging = await _dashboardService.GetAgingCarteraAsync();
        return Ok(ApiResponse<DashboardAgingCarteraDto>.Ok(aging));
    }

    // Transacciones sospechosas por rango
    // GET /api/Dashboard/transacciones-sospechosas?fechaInicio=2025-04-09&fechaFin=2025-04-16
    [HttpGet("transacciones-sospechosas")]
    public async Task<IActionResult> GetTransaccionesSospechosas(
        [FromQuery] DateTime? fechaInicio = null,
        [FromQuery] DateTime? fechaFin    = null)
    {
        var hasta = (fechaFin   ?? DateTime.Today).Date;
        var desde = (fechaInicio ?? hasta.AddDays(-6)).Date;

        var transacciones = await _dashboardService.GetTransaccionesSospechosasAsync(desde, hasta);
        return Ok(ApiResponse<IEnumerable<DashboardTransaccionSospechosaDto>>.Ok(transacciones));
    }

    // Resumen financiero de cartera
    [HttpGet("finanzas-resumen")]
    public async Task<IActionResult> GetFinanzasResumen()
    {
        var resumen = await _dashboardService.GetFinanzasResumenAsync();
        return Ok(ApiResponse<DashboardFinanzasResumenDto>.Ok(resumen));
    }
}
