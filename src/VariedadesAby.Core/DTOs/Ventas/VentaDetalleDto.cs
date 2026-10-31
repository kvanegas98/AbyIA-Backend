namespace VariedadesAby.Core.DTOs.Ventas;

public class VentaDetalleDto
{
    public int IdVenta { get; set; }
    public string CodigoFactura { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string Sucursal { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string TipoComprobante { get; set; } = string.Empty;
    public string? TipoPago { get; set; }
    public string? DescripcionPago { get; set; }
    public decimal Total { get; set; }
    public string? MotivoAnulacion { get; set; }
    public string? UsuarioAnulacion { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public IEnumerable<VentaDetalleItemDto> Items { get; set; } = [];
}

public class VentaDetalleItemDto
{
    public int IdDetalleVenta { get; set; }
    public string CodigoArticulo { get; set; } = string.Empty;
    public string Articulo { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Precio { get; set; }
    public decimal Descuento { get; set; }
    public decimal Subtotal { get; set; }
}
