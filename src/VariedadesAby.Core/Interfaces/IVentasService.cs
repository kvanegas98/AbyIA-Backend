using VariedadesAby.Core.DTOs.Ventas;

namespace VariedadesAby.Core.Interfaces;

public interface IVentasService
{
    Task AnularAsync(int idVenta, int idSucursal, AnularVentaRequestDto dto);
    Task<BuscarVentasResponseDto> BuscarAsync(BuscarVentasQueryDto filtro);
    Task<VentaDetalleDto> ObtenerDetalleAsync(int idVenta);
    Task<IEnumerable<ArticuloResumenDto>> BuscarArticulosAsync(string termino);
    Task<IEnumerable<ClienteResumenDto>> BuscarClientesAsync(string termino);
    Task<IEnumerable<FacturaPorProductoDto>> BuscarFacturasPorArticulosAsync(int[] idsArticulos, int idCliente);
}
