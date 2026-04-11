namespace VariedadesAby.Core.DTOs.Sucursal;

public class TopCapitalInmovilizadoDto
{
    public string  codigo         { get; set; } = string.Empty;
    public string  articulo       { get; set; } = string.Empty;
    public string  categoria      { get; set; } = string.Empty;
    public int     stockTotal     { get; set; }
    public decimal precioCompra   { get; set; }
    public decimal valorCosto     { get; set; }
    public int?    diasSinVenta   { get; set; } // null = nunca se ha vendido
}
