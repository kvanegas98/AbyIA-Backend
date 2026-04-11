using VariedadesAby.Core.DTOs.Dashboard;

namespace VariedadesAby.Core.Interfaces;

public interface IDashboardRepository
{
    Task<DashboardResumenDto> GetResumenAsync(int? idSucursal = null);
}
