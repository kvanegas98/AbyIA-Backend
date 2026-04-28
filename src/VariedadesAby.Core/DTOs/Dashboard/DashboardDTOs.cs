namespace VariedadesAby.Core.DTOs.Dashboard;

public class DashboardResumenDto
{
    // ── Métricas del período seleccionado ────────────────────────────────────
    public decimal VentaPeriodo                      { get; set; }
    public int     CantidadVentasPeriodo             { get; set; }
    public decimal PorcentajeVentavsPeriodoAnterior  { get; set; }

    public decimal UtilidadPeriodo                       { get; set; }
    public decimal PorcentajeUtilidadvsPeriodoAnterior   { get; set; }

    public decimal TicketPeriodo                         { get; set; }
    public decimal PorcentajeTicketvsPeriodoAnterior     { get; set; }

    // ── Snapshot actual (siempre hoy, ignoran el rango) ─────────────────────
    public decimal CuentasPorCobrar  { get; set; }
    public int     StockCritico      { get; set; }
    public int     StockGlobal       { get; set; }
    public int     ArticulosActivos  { get; set; }

    // ── Metadatos del período ────────────────────────────────────────────────
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
    public int      DiasPeriodo { get; set; }
}

public class DashboardFinanzasDto
{
    public List<string>  Fechas   { get; set; } = new();
    public List<decimal> Ingresos { get; set; } = new();
    public List<decimal> Egresos  { get; set; } = new();
}

public class DashboardTopProductoDto
{
    public string  Producto        { get; set; } = string.Empty;
    public string  Categoria       { get; set; } = string.Empty;
    public int     CantidadVendida { get; set; }
    public decimal IngresoTotal    { get; set; }
}

public class DashboardTopCategoriaDto
{
    public string  Categoria       { get; set; } = string.Empty;
    public int     CantidadVendida { get; set; }
    public decimal IngresoTotal    { get; set; }
    public int     NumeroVentas    { get; set; }
    public decimal PorcentajeDelTotal { get; set; }
}

public class DashboardVentasPorHoraDto
{
    public int     Hora      { get; set; }
    public decimal Total     { get; set; }
    public int     Cantidad  { get; set; }
}

public class DashboardSucursalResumenDto
{
    public string  Sucursal      { get; set; } = string.Empty;
    public decimal VentaTotal    { get; set; }
    public decimal UtilidadTotal { get; set; }
    public int     TotalVentas   { get; set; }
    public decimal TicketPromedio { get; set; }
}

public class DashboardAgingCarteraDto
{
    public decimal Corriente            { get; set; }
    public decimal Dias30a60            { get; set; }
    public decimal Dias61a90            { get; set; }
    public decimal MasDe90Dias          { get; set; }
    public int     ClientesCorriente    { get; set; }
    public int     ClientesDias30a60    { get; set; }
    public int     ClientesDias61a90    { get; set; }
    public int     ClientesMasDe90Dias  { get; set; }
}

public class DashboardTransaccionSospechosaDto
{
    public string    Tipo                { get; set; } = string.Empty;
    public string    Cliente             { get; set; } = "Cliente General";
    public string    Motivo              { get; set; } = string.Empty;
    public decimal   Monto               { get; set; }
    public string    Riesgo              { get; set; } = "Medio";
    public DateTime  Fecha               { get; set; }
    public string?   NumeroNotaCredito   { get; set; }
    public string?   NumeroFactura       { get; set; }
}

public class DashboardFinanzasResumenDto
{
    public decimal TotalCartera        { get; set; }
    public decimal CobrosProyectados7d { get; set; }
    public int     ClientesMorosos     { get; set; }
}
