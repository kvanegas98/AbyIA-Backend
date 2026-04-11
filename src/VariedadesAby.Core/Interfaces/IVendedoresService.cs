using VariedadesAby.Core.DTOs.Vendedores;

namespace VariedadesAby.Core.Interfaces;

public interface IVendedoresService
{
    /// <summary>
    /// Retorna KPIs globales + ranking de vendedores con desglose contado/crédito
    /// para el rango de fechas indicado. Solo incluye vendedores con ventas > 0.
    /// </summary>
    Task<PanelVendedoresDto> GetPanelAsync(FiltroVendedoresViewModel filtro);
}
