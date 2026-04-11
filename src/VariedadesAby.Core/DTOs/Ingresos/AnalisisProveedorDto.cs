namespace VariedadesAby.Core.DTOs.Ingresos;

public class AnalisisInventarioProveedorDto
{
    public int idproveedor { get; set; }
    public string proveedor { get; set; } = string.Empty;

    // ── Productos ─────────────────────────────────────────────────────────────
    /// <summary>Productos distintos que se le han comprado alguna vez</summary>
    public int totalProductos { get; set; }
    /// <summary>De esos productos, cuántos aún tienen stock > 0</summary>
    public int productosConStock { get; set; }

    // ── Unidades ──────────────────────────────────────────────────────────────
    public int totalUnidadesCompradas { get; set; }
    public int stockActualUnidades { get; set; }
    public int unidadesVendidas { get; set; }

    // ── Dinero ────────────────────────────────────────────────────────────────
    /// <summary>Total pagado a este proveedor histórico</summary>
    public decimal totalInvertido { get; set; }
    /// <summary>Valor del stock que aún no se ha vendido (a precio de compra)</summary>
    public decimal valorStockActual { get; set; }
    /// <summary>Dinero ya recuperado estimado (unidades vendidas × precio compra)</summary>
    public decimal valorRecuperado { get; set; }

    // ── Indicadores ──────────────────────────────────────────────────────────
    /// <summary>% de unidades vendidas sobre el total comprado</summary>
    public decimal porcentajeRotacion { get; set; }
    public DateTime? ultimaCompra { get; set; }

    /// <summary>Promedio de días sin venta de los productos de este proveedor</summary>
    public int diasSinMovimientoPromedio { get; set; }
    /// <summary>Clasificación: Fresco (0-15d), Lento (16-45d), Dormido (46-90d), Muerto (+90d)</summary>
    public string clasificacionInventario { get; set; } = string.Empty;

    /// <summary>Recomendación automática basada en rotación y días sin movimiento</summary>
    public string recomendacion { get; set; } = string.Empty;
    /// <summary>Color semáforo: verde, amarillo, rojo, negro</summary>
    public string semaforo { get; set; } = string.Empty;
}
