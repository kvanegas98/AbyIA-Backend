namespace VariedadesAby.Core.DTOs.Ventas;

public class BuscarVentasQueryDto
{
    public string? CodigoFactura { get; set; }
    public string? Cliente { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public string? TipoComprobante { get; set; }
    public int? SucursalId { get; set; }
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 20;
}
