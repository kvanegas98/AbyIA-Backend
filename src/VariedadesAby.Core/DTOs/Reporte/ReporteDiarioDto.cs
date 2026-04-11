namespace VariedadesAby.Core.DTOs.Reporte;

public class ReporteDiarioDto
{
    public DateTime Fecha               { get; init; }
    public ResumenVentasDto Ventas      { get; init; } = new();
    public IEnumerable<VentaSucursalDto> VentasPorSucursal { get; init; } = [];
    public ResumenSimpleDto Devoluciones { get; init; } = new();
    public ResumenSimpleDto Abonos       { get; init; } = new();
    public ResumenCarteraDto Cartera     { get; init; } = new();
    public IEnumerable<TopProductoReporteDto> TopProductos { get; init; } = [];
}

public class ResumenVentasDto
{
    public int     TotalVentas  { get; init; }
    public decimal TotalGlobal  { get; init; }
    public decimal TotalContado { get; init; }
    public decimal TotalCredito { get; init; }
}

public class VentaSucursalDto
{
    public string  Sucursal      { get; init; } = string.Empty;
    public int     VentasContado { get; init; }
    public decimal TotalContado  { get; init; }
    public int     VentasCredito { get; init; }
    public decimal TotalCredito  { get; init; }
    public int     TotalVentas   { get; init; }
    public decimal TotalGlobal   { get; init; }
}

public class ResumenSimpleDto
{
    public int     Cantidad   { get; init; }
    public decimal MontoTotal { get; init; }
}

public class ResumenCarteraDto
{
    public int     ClientesConSaldo { get; init; }
    public decimal SaldoTotal       { get; init; }
}

public class TopProductoReporteDto
{
    public string  Producto        { get; init; } = string.Empty;
    public int     CantidadVendida { get; init; }
    public decimal TotalVendido    { get; init; }
}

public record ReporteEnvioResultado(bool Enviado, string Mensaje, int TotalVentas, decimal TotalGlobal);
