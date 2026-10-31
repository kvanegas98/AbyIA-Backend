namespace VariedadesAby.Core.DTOs.Ventas;

public class VentaResumenDto
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
    public decimal Total { get; set; }
    public int TotalRegistros { get; set; }
}
