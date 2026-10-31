namespace VariedadesAby.Core.DTOs.Usuarios;

public class UsuarioSucursalDto
{
    public int IdUsuario { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public int? IdSucursal { get; set; }
    public string? NombreSucursal { get; set; }
    public DateTime? FechaAsignacion { get; set; }
}

public class SucursalSimpleDto
{
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class AsignarSucursalRequestDto
{
    public int IdSucursal { get; set; }
}

public class ReasignarSucursalRequestDto
{
    public int IdSucursalVieja { get; set; }
    public int IdSucursalNueva { get; set; }
}

public class SucursalAsignadaDto
{
    public int IdSucursal { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public DateTime? FechaAsignacion { get; set; }
    public bool EsDefault { get; set; }
}
