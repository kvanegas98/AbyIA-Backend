using VariedadesAby.Core.DTOs.Analisis;

namespace VariedadesAby.Core.Interfaces;

public interface IAnalisisService
{
    Task<ClientasOroResultadoDto>            GetClientasOroAsync(FiltroAnalisisViewModel filtro);
    Task<VelocidadProductosResultadoDto>     GetVelocidadProductosAsync(FiltroAnalisisViewModel filtro);
    Task<RendimientoProveedoresResultadoDto> GetRendimientoProveedoresAsync(FiltroAnalisisViewModel filtro);
}
