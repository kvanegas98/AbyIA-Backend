namespace VariedadesAby.Core.DTOs.Vendedores;

/// <summary>Respuesta completa del panel: KPIs globales + detalle por vendedor.</summary>
public class PanelVendedoresDto
{
    public KpisVendedoresDto Kpis { get; set; } = new();
    public List<VendedorResumenDto> Vendedores { get; set; } = new();
}

/// <summary>KPIs globales del rango seleccionado (todos los vendedores activos).</summary>
public class KpisVendedoresDto
{
    public decimal TotalGeneral { get; set; }
    public decimal TotalContado { get; set; }
    public decimal TotalCredito { get; set; }
    public decimal TotalUtilidad { get; set; }
    public int TotalVentas { get; set; }
    public int TotalVentasContado { get; set; }
    public int TotalVentasCredito { get; set; }

    /// <summary>Porcentaje del total que corresponde a ventas de contado.</summary>
    public decimal PorcentajeContado => TotalGeneral > 0
        ? Math.Round(TotalContado / TotalGeneral * 100, 1) : 0;

    /// <summary>Porcentaje del total que corresponde a ventas a crédito.</summary>
    public decimal PorcentajeCredito => TotalGeneral > 0
        ? Math.Round(TotalCredito / TotalGeneral * 100, 1) : 0;
}

/// <summary>Resumen de ventas de un vendedor en el rango seleccionado.</summary>
public class VendedorResumenDto
{
    public int IdUsuario { get; set; }
    public string Nombre { get; set; } = string.Empty;

    // ── Contado ──────────────────────────────────────────────────────────
    public decimal TotalContado { get; set; }
    public int VentasContado { get; set; }

    // ── Crédito ──────────────────────────────────────────────────────────
    public decimal TotalCredito { get; set; }
    public int VentasCredito { get; set; }

    // ── Totales ──────────────────────────────────────────────────────────
    public decimal Total { get; set; }
    public int TotalVentas { get; set; }
    public decimal Utilidad { get; set; }

    /// <summary>Ticket promedio (contado + crédito).</summary>
    public decimal TicketPromedio => TotalVentas > 0
        ? Math.Round(Total / TotalVentas, 2) : 0;

    /// <summary>% del total del vendedor que es crédito — indicador de riesgo.</summary>
    public decimal PorcentajeCredito => Total > 0
        ? Math.Round(TotalCredito / Total * 100, 1) : 0;

    /// <summary>Semáforo de riesgo crediticio: Verde / Amarillo / Rojo.</summary>
    public string NivelRiesgo => PorcentajeCredito switch
    {
        <= 20 => "Verde",
        <= 50 => "Amarillo",
        _     => "Rojo"
    };
}
