using System.ComponentModel.DataAnnotations;

namespace VariedadesAby.Core.DTOs.Ingresos;

public class CrearIngresoViewModel
{
    [Required] public int idproveedor { get; set; }
    [Required] public int idusuario { get; set; }
    public int idSucursal { get; set; }
    [Required] public string tipo_comprobante { get; set; } = string.Empty;
    public string? serie_comprobante { get; set; }
    [Required] public string num_comprobante { get; set; } = string.Empty;
    [Required] public decimal impuesto { get; set; }
    [Required] public decimal total { get; set; }
    [Required] public List<DetalleIngresoViewModel> detalles { get; set; } = [];
    public List<string>? urlsImagenes { get; set; } = [];
    public string? modeloIa { get; set; }
}

public class DetalleIngresoViewModel
{
    [Required] public int idarticulo { get; set; }
    public string? articulo { get; set; }
    [Required] public int cantidad { get; set; }
    [Required] public decimal precio { get; set; }
    public decimal? precio_venta { get; set; }
    public string? codigo { get; set; }
    public string? nombreArticulo { get; set; }
    public string? descripcionArticulo { get; set; }
    public int? idCategoria { get; set; }
}
