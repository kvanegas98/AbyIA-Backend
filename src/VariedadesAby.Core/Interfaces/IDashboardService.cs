using VariedadesAby.Core.DTOs.Dashboard;

namespace VariedadesAby.Core.Interfaces;

public interface IDashboardService
{
    Task<DashboardResumenDto>                              GetResumenAsync(DateTime? desde = null, DateTime? hasta = null);
    Task<DashboardFinanzasDto>                             GetFinanzasAsync(DateTime desde, DateTime hasta);
    Task<IEnumerable<DashboardTopProductoDto>>             GetTopProductosAsync(int top, DateTime desde, DateTime hasta);
    Task<IEnumerable<DashboardTopCategoriaDto>>            GetTopCategoriasAsync(int top, DateTime desde, DateTime hasta);
    Task<IEnumerable<DashboardVentasPorHoraDto>>           GetVentasPorHoraAsync(DateTime fecha);
    Task<IEnumerable<DashboardSucursalResumenDto>>         GetVentasPorSucursalAsync(DateTime desde, DateTime hasta);
    Task<DashboardAgingCarteraDto>                         GetAgingCarteraAsync();
    Task<IEnumerable<DashboardTransaccionSospechosaDto>>   GetTransaccionesSospechosasAsync(DateTime desde, DateTime hasta);
    Task<DashboardFinanzasResumenDto>                      GetFinanzasResumenAsync();
}
