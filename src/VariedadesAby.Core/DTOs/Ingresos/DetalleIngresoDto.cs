namespace VariedadesAby.Core.DTOs.Ingresos;

public class IngresoDetalleDto
{
    // ── Encabezado ────────────────────────────────────────────────────────────
    public int idingreso { get; set; }
    public string proveedor { get; set; } = string.Empty;
    public string? telefonoProveedor { get; set; }
    public string usuario { get; set; } = string.Empty;
    public string sucursal { get; set; } = string.Empty;
    public string tipo_comprobante { get; set; } = string.Empty;
    public string? serie_comprobante { get; set; }
    public string num_comprobante { get; set; } = string.Empty;
    public DateTime fecha_hora { get; set; }
    public decimal impuesto { get; set; }
    public decimal subtotal { get; set; }
    public decimal totalImpuesto { get; set; }
    public decimal total { get; set; }
    public string estado { get; set; } = string.Empty;

    // ── Artículos ─────────────────────────────────────────────────────────────
    public List<ItemIngresoDto> items { get; set; } = [];
    public List<string> urlsImagenes { get; set; } = [];
}

public class ItemIngresoDto
{
    public int iddetalle { get; set; }
    public string codigo { get; set; } = string.Empty;
    public string articulo { get; set; } = string.Empty;
    public int cantidad { get; set; }
    public decimal precio { get; set; }
    public decimal? precio_venta { get; set; }
    public decimal subtotal { get; set; }
}
