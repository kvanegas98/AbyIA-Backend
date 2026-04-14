using VariedadesAby.Core.DTOs.Ingresos;

namespace VariedadesAby.Core.Interfaces;

public interface IIngresosService
{
    Task<(int idIngreso, string numComprobante)> CrearAsync(CrearIngresoViewModel model);
    Task<PagedResult<IngresoListadoDto>> ListarAsync(FiltroIngresosViewModel filtro);
    Task<IngresoDetalleDto> ObtenerDetalleAsync(int idIngreso);
    Task<byte[]> GenerarPdfAsync(int idIngreso);
    Task<PagedResult<AnalisisInventarioProveedorDto>> AnalisisInventarioPorProveedorAsync(FiltroAnalisisProveedorViewModel filtro);
    Task<PagedResult<AnalisisProveedorDetalleDto>> AnalisisDetalleProveedorAsync(int idProveedor, FiltroAnalisisProveedorViewModel filtro);
    Task<byte[]> GenerarPdfDetalleProveedorAsync(int idProveedor, FiltroAnalisisProveedorViewModel filtro);
    Task<bool> AnularAsync(int idIngreso);
}
