namespace VariedadesAby.Core.DTOs.Ventas;

public class ArticuloResumenDto
{
    public int IdArticulo { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public decimal PrecioVenta { get; set; }
    public decimal PrecioCompra { get; set; }
    public bool Condicion { get; set; }
}
