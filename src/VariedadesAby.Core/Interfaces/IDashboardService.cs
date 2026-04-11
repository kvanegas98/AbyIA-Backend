using VariedadesAby.Core.DTOs.Dashboard;

namespace VariedadesAby.Core.Interfaces;

public interface IDashboardService
{
    Task<DashboardResumenDto> GetResumenAsync();
    Task<DashboardFinanzasDto> GetFinanzasAsync(int dias = 30);
    Task<IEnumerable<DashboardTopProductoDto>> GetTopProductosAsync(int top = 5);
    Task<IEnumerable<DashboardTransaccionSospechosaDto>> GetTransaccionesSospechosasAsync(int dias = 7);
    Task<DashboardFinanzasResumenDto> GetFinanzasResumenAsync();
}
