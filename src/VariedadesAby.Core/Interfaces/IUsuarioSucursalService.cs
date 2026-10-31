using VariedadesAby.Core.DTOs.Usuarios;

namespace VariedadesAby.Core.Interfaces;

public interface IUsuarioSucursalService
{
    Task<IEnumerable<UsuarioSucursalDto>> ListarAsync();
    Task<IEnumerable<SucursalSimpleDto>> ListarSucursalesAsync();
    Task<IEnumerable<SucursalAsignadaDto>> ListarSucursalesUsuarioAsync(int idUsuario);
    Task AsignarAsync(int idUsuario, int idSucursal);
    Task ReasignarAsync(int idUsuario, int idSucursalVieja, int idSucursalNueva);
}
