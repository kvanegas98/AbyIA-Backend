namespace VariedadesAby.Core.DTOs.Dashboard;

public class DashboardResumenDto
{
    public decimal VentaNetaHoy { get; set; }
    public decimal PorcentajeVentaNetavsAyer { get; set; }

    public decimal CuentasPorCobrar { get; set; }
    public decimal PorcentajeCuentasPorCobrarvsAyer { get; set; } // O vs mes pasado

    public decimal TicketPromedio { get; set; }
    public decimal PorcentajeTicketPromediovsAyer { get; set; }

    public int StockCritico { get; set; }
    public decimal PorcentajeStockCriticovsAyer { get; set; }
}

public class DashboardFinanzasDto
{
    public List<string> Fechas { get; set; } = new();
    public List<decimal> Ingresos { get; set; } = new();
    public List<decimal> Egresos { get; set; } = new();
}

public class DashboardTopProductoDto
{
    public string Producto { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty; // Opcional, para contexto
    public int CantidadVendida { get; set; }
    public decimal IngresoTotal { get; set; }
}

public class DashboardTransaccionSospechosaDto
{
    public string Tipo { get; set; } = string.Empty; // "Nota de Crédito", "Anulación", "Descuento Alto"
    public string Cliente { get; set; } = "Cliente General";
    public string Motivo { get; set; } = string.Empty;
    public decimal Monto { get; set; }
    public string Riesgo { get; set; } = "Medio"; // Alto, Medio, Bajo
    public DateTime Fecha { get; set; }
}

public class DashboardFinanzasResumenDto
{
    public decimal TotalCartera         { get; set; }
    public decimal CobrosProyectados7d  { get; set; }
    public int     ClientesMorosos      { get; set; }
}
