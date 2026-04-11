using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Core.Wrappers;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class FinanzasController : ControllerBase
{
    private readonly IFinanzasRepository _finanzasRepository;

    public FinanzasController(IFinanzasRepository finanzasRepository)
    {
        _finanzasRepository = finanzasRepository;
    }

    // GET: api/Finanzas/CarteraClientes
    [AllowAnonymous]
    [HttpGet("[action]")]
    public async Task<IActionResult> CarteraClientes()
    {
        var cartera = await _finanzasRepository.GetCarteraClientesAsync();
        return Ok(ApiResponse<object>.Ok(cartera));
    }

    // GET: api/Finanzas/ObtenerAbonos
    [HttpGet("[action]")]
    public async Task<IActionResult> ObtenerAbonos()
    {
        var abonos = await _finanzasRepository.GetAbonosAsync();
        return Ok(ApiResponse<object>.Ok(abonos));
    }

    // GET: api/Finanzas/EstadoCuenta/5/2025-01-01/2025-12-31
    [HttpGet("[action]/{idCliente}/{fechaInicio}/{fechaFin}")]
    public async Task<IActionResult> EstadoCuenta(int idCliente, DateTime fechaInicio, DateTime fechaFin)
    {
        var movimientos = await _finanzasRepository.GetEstadoCuentaClienteAsync(idCliente, fechaInicio, fechaFin);
        return Ok(ApiResponse<object>.Ok(movimientos));
    }

    // GET: api/Finanzas/Resumen
    [AllowAnonymous]
    [HttpGet("[action]")]
    public async Task<IActionResult> Resumen()
    {
        var resumen = await _finanzasRepository.GetResumenFinanzasAsync();
        return Ok(ApiResponse<object>.Ok(resumen));
    }

    // GET: api/Finanzas/FlujoCaja?dias=30
    [AllowAnonymous]
    [HttpGet("[action]")]
    public async Task<IActionResult> FlujoCaja([FromQuery] int dias = 30)
    {
        var flujo = await _finanzasRepository.GetFlujoCajaAsync(dias);
        return Ok(ApiResponse<object>.Ok(flujo));
    }
}
