namespace VariedadesAby.Core.DTOs.Sucursal;

public class TendenciaSucursalDto
{
    public int      idsucursal { get; set; }
    public string   sucursal   { get; set; } = string.Empty;
    public DateTime fecha      { get; set; }
    public decimal  ventaDia   { get; set; }
}
