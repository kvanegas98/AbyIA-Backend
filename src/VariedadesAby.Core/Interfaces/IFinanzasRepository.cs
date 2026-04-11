using VariedadesAby.Core.DTOs.Finanzas;

namespace VariedadesAby.Core.Interfaces;

public interface IFinanzasRepository
{
    Task<IEnumerable<CarteraClienteDto>> GetCarteraClientesAsync();
    Task<IEnumerable<AbonoDetalleDto>> GetAbonosAsync();
    Task<IEnumerable<EstadoCuentaMovimientoDto>> GetEstadoCuentaClienteAsync(int idCliente, DateTime fechaInicio, DateTime fechaFin);
    Task<FinanzasResumenDto> GetResumenFinanzasAsync();
    Task<IEnumerable<FinanzasFlujoCajaDto>> GetFlujoCajaAsync(int dias);
}
