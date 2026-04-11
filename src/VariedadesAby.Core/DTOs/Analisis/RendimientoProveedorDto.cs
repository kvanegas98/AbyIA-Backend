namespace VariedadesAby.Core.DTOs.Analisis;

public class RendimientoProveedorDto
{
    public int     idproveedor    { get; set; }
    public string  proveedor      { get; set; } = string.Empty;
    public int     totalProductos { get; set; }

    // ── Inversión ────────────────────────────────────────────────────────────
    /// <summary>Total pagado al proveedor (histórico)</summary>
    public decimal totalInvertido   { get; set; }
    /// <summary>Costo de las unidades ya vendidas (unidades vendidas × precio compra promedio)</summary>
    public decimal costoVendido     { get; set; }
    /// <summary>Valor del stock que aún no se ha vendido (a precio de compra)</summary>
    public decimal valorStockActual { get; set; }

    // ── Ingresos reales ───────────────────────────────────────────────────────
    /// <summary>Ingreso real generado por ventas de productos de este proveedor (precio venta − descuento)</summary>
    public decimal ingresoReal { get; set; }

    // ── Rentabilidad ─────────────────────────────────────────────────────────
    /// <summary>ingresoReal − costoVendido</summary>
    public decimal margenBruto      { get; set; }
    /// <summary>margenBruto / ingresoReal × 100</summary>
    public decimal margenPorcentaje { get; set; }
    /// <summary>margenBruto / totalInvertido × 100 — retorno sobre la inversión total</summary>
    public decimal roi              { get; set; }

    // ── Velocidad ─────────────────────────────────────────────────────────────
    /// <summary>% de unidades vendidas sobre el total comprado</summary>
    public decimal porcentajeRotacion       { get; set; }
    /// <summary>Promedio de días entre la fecha de compra al proveedor y la venta al cliente</summary>
    public int     diasRecuperacionPromedio { get; set; }
    public DateTime? ultimaCompra          { get; set; }

    // ── Clasificación (calculada en C#) ──────────────────────────────────────
    /// <summary>Estrella · Rentable · Regular · Revisar</summary>
    public string clasificacion { get; set; } = string.Empty;
    public string semaforo      { get; set; } = string.Empty;
    public string recomendacion { get; set; } = string.Empty;
}
