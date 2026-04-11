namespace VariedadesAby.Core.DTOs.Ingresos;

public class FiltroIngresosViewModel
{
    public string? busqueda { get; set; }       // num_comprobante o nombre proveedor
    public string? estado { get; set; }         // CANCELADO | PENDIENTE
    public int? idSucursal { get; set; }
    public int? idProveedor { get; set; }
    public DateTime? fechaDesde { get; set; }
    public DateTime? fechaHasta { get; set; }
    public int pagina { get; set; } = 1;
    public int porPagina { get; set; } = 20;
}

public class IngresoListadoDto
{
    public int idingreso { get; set; }
    public string proveedor { get; set; } = string.Empty;
    public string usuario { get; set; } = string.Empty;
    public string sucursal { get; set; } = string.Empty;
    public string tipo_comprobante { get; set; } = string.Empty;
    public string? serie_comprobante { get; set; }
    public string num_comprobante { get; set; } = string.Empty;
    public DateTime fecha_hora { get; set; }
    public decimal impuesto { get; set; }
    public decimal total { get; set; }
    public string estado { get; set; } = string.Empty;
    public int totalArticulos { get; set; }
}

public class FiltroAnalisisProveedorViewModel
{
    public string? busqueda  { get; set; }
    public int     pagina    { get; set; } = 1;
    public int     porPagina { get; set; } = 20;
}

public class PagedResult<T>
{
    public IEnumerable<T> data { get; set; } = [];
    public int totalRegistros { get; set; }
    public int pagina { get; set; }
    public int porPagina { get; set; }
    public int totalPaginas => (int)Math.Ceiling((double)totalRegistros / porPagina);
}
