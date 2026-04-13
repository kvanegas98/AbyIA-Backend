namespace VariedadesAby.Core.DTOs.Compras;

public class ValidarArticuloDto
{
    public bool     EsNuevo      { get; init; }
    public int      IdArticulo   { get; init; }
    public string?  Nombre       { get; init; }
    public string?  Codigo       { get; init; }
    public int?     IdCategoria  { get; init; }
    public decimal? PrecioVenta  { get; init; }
    public decimal? PrecioCompra { get; init; }
}

public class AnalizarPdfResponseDto
{
    public CompraExtraidaDto compra { get; set; } = new();
    public List<string> urlsImagenesCloudinary { get; set; } = new();
    public string? proveedorDetectado { get; set; }
    public List<string> advertencias { get; set; } = new();
    public string? modeloUsado { get; set; }
}

/// <summary>
/// Estructura idéntica a CrearIngresoViewModel para que el frontend
/// pueda enviar compra.compra directamente al endpoint Crear sin mapeo.
/// </summary>
public class CompraExtraidaDto
{
    public int idproveedor { get; set; }
    public int idusuario { get; set; }
    public int idSucursal { get; set; }
    public string tipo_comprobante { get; set; } = "CONTADO";
    public string? serie_comprobante { get; set; }
    public string num_comprobante { get; set; } = string.Empty;
    public decimal impuesto { get; set; }
    public decimal total { get; set; }
    public List<DetalleCompraExtraidoDto> detalles { get; set; } = new();
}

public class DetalleCompraExtraidoDto
{
    public int idarticulo { get; set; }
    public string? articulo { get; set; }
    public int cantidad { get; set; }
    public decimal precio { get; set; }
    public decimal? precio_venta { get; set; }
    public string? codigo { get; set; }
    public string? codigoBarras { get; set; }
    public string? nombreArticulo { get; set; }
    public string? descripcionArticulo { get; set; }
    public int? idCategoria { get; set; }
    /// <summary>Solo informativo — extraído de la factura para visualización</summary>
    public string? marca { get; set; }
}
