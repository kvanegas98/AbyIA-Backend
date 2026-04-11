namespace VariedadesAby.Core.DTOs.Ingresos;

public class AnalisisProveedorDetalleDto
{
    public string codigo { get; set; } = string.Empty;
    public string articulo { get; set; } = string.Empty;
    public string? categoria { get; set; }
    public int totalComprado { get; set; }
    public int stockActual { get; set; }
    public int unidadesVendidas { get; set; }
    public decimal porcentajeRotacion { get; set; }
    public decimal precioCompra { get; set; }
    public decimal? precioVenta { get; set; }
    public decimal valorStockActual { get; set; }
    public decimal totalInvertido { get; set; }
    public DateTime ultimaCompra { get; set; }
    /// <summary>Fecha de la última venta de este artículo (null si nunca se ha vendido)</summary>
    public DateTime? ultimaVenta { get; set; }
    /// <summary>Días desde la última venta (o desde la compra si nunca se ha vendido)</summary>
    public int diasSinMovimiento { get; set; }
    /// <summary>Clasificación: Fresco, Lento, Dormido, Muerto</summary>
    public string clasificacionInventario { get; set; } = string.Empty;
    public string semaforo { get; set; } = string.Empty;
}
