namespace VariedadesAby.Core.DTOs.Ventas;

public class FacturaPorProductoDto
{
    public int IdVenta { get; set; }
    public string CodigoFactura { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string TipoComprobante { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public List<ProductoEnFacturaDto> ProductosEncontrados { get; set; } = [];
}

public class ProductoEnFacturaDto
{
    public int IdArticulo { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public decimal Precio { get; set; }
    public decimal Descuento { get; set; }
    public List<DevolucionProductoDto> Devoluciones { get; set; } = [];
}

public class DevolucionProductoDto
{
    public string CodigoNota { get; set; } = string.Empty;
    public string TipoNotaCredito { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public string? UsuarioDevolucion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public string? EstadoNota { get; set; }
    // Detalle por producto (DetalleNotaCredito)
    public int Cantidad { get; set; }
    public decimal Precio { get; set; }
    public decimal Descuento { get; set; }
    public decimal Total { get; set; }
}
