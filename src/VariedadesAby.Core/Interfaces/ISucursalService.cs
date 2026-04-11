using VariedadesAby.Core.DTOs.Sucursal;

namespace VariedadesAby.Core.Interfaces;

public interface ISucursalService
{
    /// <summary>Comparativo de rendimiento entre sucursales vs el periodo anterior de igual duración.</summary>
    Task<IEnumerable<RendimientoSucursalDto>> GetRendimientoAsync(DateTime fechaDesde, DateTime fechaHasta);

    /// <summary>Ventas diarias por sucursal para gráfico de tendencia.</summary>
    Task<IEnumerable<TendenciaSucursalDto>> GetTendenciaAsync(DateTime fechaDesde, DateTime fechaHasta);

    /// <summary>Top N productos más vendidos por sucursal.</summary>
    Task<IEnumerable<TopProductoSucursalDto>> GetTopProductosAsync(DateTime fechaDesde, DateTime fechaHasta, int top = 5);

    /// <summary>Inventario valorizado actual por sucursal: stock, costo y precio de venta.</summary>
    Task<InventarioValorizadoResultadoDto> GetInventarioValorizadoAsync();

    /// <summary>Inventario valorizado agrupado por categoría, ordenado de mayor a menor capital.</summary>
    Task<IEnumerable<InventarioCategoriaDto>> GetInventarioCategoriaAsync();

    /// <summary>Top 10 artículos con mayor capital inmovilizado (stock × precio_compra).</summary>
    Task<IEnumerable<TopCapitalInmovilizadoDto>> GetTopCapitalInmovilizadoAsync();
}
