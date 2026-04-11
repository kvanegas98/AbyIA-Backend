namespace VariedadesAby.Core.DTOs.Analisis;

public class FiltroAnalisisViewModel
{
    public string? busqueda  { get; set; }
    public int     pagina    { get; set; } = 1;
    public int     porPagina { get; set; } = 20;
}

// ─── Resúmenes KPI ────────────────────────────────────────────────────────────

public class ClientasOroResumenDto
{
    public int     totalClientes        { get; set; }
    public int     clientesVip          { get; set; }
    public int     enRiesgo             { get; set; }
    public decimal ticketPromedioGeneral { get; set; }
}

public class VelocidadResumenDto
{
    public int     totalProductos   { get; set; }
    public int     volando          { get; set; }
    public int     detenidos        { get; set; }
    public int     quiebreInminente { get; set; }
    public decimal margenPromedio   { get; set; }
}

public class RendimientoResumenDto
{
    public int     totalProveedores     { get; set; }
    public int     proveedoresEstrella  { get; set; }
    public decimal totalInvertido       { get; set; }
    public decimal ingresoTotal         { get; set; }
    public decimal roiPromedio          { get; set; }
}

// ─── Respuestas paginadas con resumen ─────────────────────────────────────────

public class ClientasOroResultadoDto
{
    public ClientasOroResumenDto      resumen        { get; set; } = new();
    public IEnumerable<ClientaOroDto> data           { get; set; } = [];
    public int                        totalRegistros { get; set; }
    public int                        pagina         { get; set; }
    public int                        porPagina      { get; set; }
    public int totalPaginas => (int)Math.Ceiling((double)totalRegistros / porPagina);
}

public class VelocidadProductosResultadoDto
{
    public VelocidadResumenDto              resumen        { get; set; } = new();
    public IEnumerable<VelocidadProductoDto> data          { get; set; } = [];
    public int                              totalRegistros { get; set; }
    public int                              pagina         { get; set; }
    public int                              porPagina      { get; set; }
    public int totalPaginas => (int)Math.Ceiling((double)totalRegistros / porPagina);
}

public class RendimientoProveedoresResultadoDto
{
    public RendimientoResumenDto                resumen        { get; set; } = new();
    public IEnumerable<RendimientoProveedorDto> data           { get; set; } = [];
    public int                                  totalRegistros { get; set; }
    public int                                  pagina         { get; set; }
    public int                                  porPagina      { get; set; }
    public int totalPaginas => (int)Math.Ceiling((double)totalRegistros / porPagina);
}
