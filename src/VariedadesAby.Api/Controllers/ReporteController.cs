using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ReporteController : ControllerBase
{
    private readonly IReporteDiarioService _reporteService;

    public ReporteController(IReporteDiarioService reporteService)
    {
        _reporteService = reporteService;
    }

    /// <summary>
    /// Genera y envía el reporte diario de operaciones de forma manual.
    /// Solo se envía si hay ventas registradas en el día (hora Nicaragua).
    /// </summary>
    // POST: api/Reporte/EnviarAhora
    [HttpPost("[action]")]
    public async Task<IActionResult> EnviarAhora()
    {
        var resultado = await _reporteService.GenerarYEnviarAsync();
        return Ok(new
        {
            enviado      = resultado.Enviado,
            mensaje      = resultado.Mensaje,
            totalVentas  = resultado.TotalVentas,
            totalGlobal  = resultado.TotalGlobal
        });
    }
}
