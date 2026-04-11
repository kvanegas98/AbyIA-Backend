namespace VariedadesAby.Core.DTOs.Sucursal;

public class TopProductoSucursalDto
{
    public int     idsucursal       { get; set; }
    public string  sucursal         { get; set; } = string.Empty;
    public int     ranking          { get; set; }
    public string  codigo           { get; set; } = string.Empty;
    public string  articulo         { get; set; } = string.Empty;
    public string  categoria        { get; set; } = string.Empty;
    public int     unidadesVendidas { get; set; }
    public decimal ingresoGenerado  { get; set; }
}
