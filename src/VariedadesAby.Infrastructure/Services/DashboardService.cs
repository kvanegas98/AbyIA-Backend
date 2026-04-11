using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using VariedadesAby.Core.DTOs.Dashboard;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IDbConnection _db;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(IDbConnection dbConnection, ILogger<DashboardService> logger)
    {
        _db = dbConnection;
        _logger = logger;
    }

    // ── sp_Dashboard_Resumen ─────────────────────────────────────────────────
    public async Task<DashboardResumenDto> GetResumenAsync()
    {
        var hoy       = DateTime.Today;
        var ayer      = hoy.AddDays(-1);
        var manana    = hoy.AddDays(1);
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

        const string sql = @"
            SELECT
                -- Ventas hoy
                ISNULL((SELECT SUM(total) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Hoy AND fecha_hora < @Manana
                           AND estado != 'Anulado'), 0) AS VentaHoy,
                ISNULL((SELECT COUNT(*)  FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Hoy AND fecha_hora < @Manana
                           AND estado != 'Anulado'), 0) AS CantidadVentasHoy,
                -- Ventas ayer
                ISNULL((SELECT SUM(total) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Ayer AND fecha_hora < @Hoy
                           AND estado != 'Anulado'), 0) AS VentaAyer,
                ISNULL((SELECT COUNT(*)  FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Ayer AND fecha_hora < @Hoy
                           AND estado != 'Anulado'), 0) AS CantidadVentasAyer,
                -- Utilidad mes
                ISNULL((SELECT SUM(utilidad) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @InicioMes
                           AND estado != 'Anulado'), 0) AS UtilidadMes,
                -- Stock
                ISNULL((SELECT SUM(stock) FROM dbo.sucursalArticulo WITH (NOLOCK)), 0) AS StockGlobal,
                ISNULL((SELECT COUNT(*) FROM dbo.articulo WITH (NOLOCK) WHERE condicion = 1), 0) AS ArticulosActivos,
                ISNULL((
                    SELECT COUNT(DISTINCT sa.idarticulo)
                    FROM dbo.sucursalArticulo sa WITH (NOLOCK)
                    INNER JOIN dbo.articulo a WITH (NOLOCK) ON sa.idarticulo = a.idarticulo
                    WHERE sa.stock <= 5 AND a.condicion = 1
                ), 0) AS StockCritico,
                -- Cuentas por cobrar
                ISNULL((
                    SELECT SUM(
                        COALESCE(ventas.TotalVentas, 0)
                        - COALESCE(abonos.TotalAbonos, 0)
                        - COALESCE(notas.TotalNotas,   0)
                    )
                    FROM dbo.credito cr WITH (NOLOCK)
                    INNER JOIN (
                        SELECT v.IdCredito, SUM(v.total) AS TotalVentas
                        FROM dbo.venta v WITH (NOLOCK)
                        WHERE v.estado != 'Anulado' AND v.IdCredito IS NOT NULL
                        GROUP BY v.IdCredito
                    ) ventas ON ventas.IdCredito = cr.Id_Credito
                    LEFT JOIN (
                        SELECT ab.Id_Credito, SUM(ab.Monto) AS TotalAbonos
                        FROM dbo.abono ab WITH (NOLOCK)
                        WHERE ab.Id_Estado = 0
                        GROUP BY ab.Id_Credito
                    ) abonos ON abonos.Id_Credito = cr.Id_Credito
                    LEFT JOIN (
                        SELECT v.IdCredito, SUM(nc.Total) AS TotalNotas
                        FROM dbo.NotaCredito nc WITH (NOLOCK)
                        INNER JOIN dbo.venta v WITH (NOLOCK) ON v.idventa = nc.IdVenta
                        WHERE nc.estado != 'Anulado' AND v.IdCredito IS NOT NULL
                        GROUP BY v.IdCredito
                    ) notas ON notas.IdCredito = cr.Id_Credito
                    WHERE cr.Id_Estado = 1
                ), 0) AS CuentasPorCobrar";

        var p = new DynamicParameters();
        p.Add("Hoy",       hoy);
        p.Add("Ayer",      ayer);
        p.Add("Manana",    manana);
        p.Add("InicioMes", inicioMes);

        var raw = await _db.QueryFirstOrDefaultAsync<DashboardResumenRaw>(sql, p);
        if (raw == null)
        {
            _logger.LogWarning("Dashboard resumen returned null");
            return new DashboardResumenDto();
        }

        decimal ticketHoy  = raw.CantidadVentasHoy  > 0 ? raw.VentaHoy  / raw.CantidadVentasHoy  : 0;
        decimal ticketAyer = raw.CantidadVentasAyer > 0 ? raw.VentaAyer / raw.CantidadVentasAyer : 0;

        return new DashboardResumenDto
        {
            VentaNetaHoy                     = raw.VentaHoy,
            CuentasPorCobrar                 = raw.CuentasPorCobrar,
            TicketPromedio                   = ticketHoy,
            StockCritico                     = raw.StockCritico,
            PorcentajeVentaNetavsAyer        = CalcularVariacion(raw.VentaHoy, raw.VentaAyer),
            PorcentajeTicketPromediovsAyer   = CalcularVariacion(ticketHoy, ticketAyer),
            PorcentajeCuentasPorCobrarvsAyer = 0,
            PorcentajeStockCriticovsAyer     = 0
        };
    }

    // ── sp_Dashboard_Finanzas ────────────────────────────────────────────────
    public async Task<DashboardFinanzasDto> GetFinanzasAsync(int dias = 30)
    {
        var desde = DateTime.Today.AddDays(-dias);
        var hasta = DateTime.Today.AddDays(1);

        const string sql = @"
            SELECT CAST(v.fecha_hora AS DATE) AS Fecha, SUM(v.total) AS Total
            FROM dbo.venta v WITH (NOLOCK)
            WHERE v.fecha_hora >= @Desde AND v.fecha_hora < @Hasta
              AND v.estado != 'Anulado'
            GROUP BY CAST(v.fecha_hora AS DATE)
            ORDER BY Fecha;

            SELECT CAST(i.fecha_hora AS DATE) AS Fecha, SUM(i.total) AS Total
            FROM dbo.ingreso i WITH (NOLOCK)
            WHERE i.fecha_hora >= @Desde AND i.fecha_hora < @Hasta
              AND i.estado != 'Anulado'
            GROUP BY CAST(i.fecha_hora AS DATE)
            ORDER BY Fecha;";

        var dto = new DashboardFinanzasDto();
        for (int i = dias - 1; i >= 0; i--)
            dto.Fechas.Add(DateTime.Today.AddDays(-i).ToString("dd MMM"));

        using var multi = await _db.QueryMultipleAsync(sql, new { Desde = desde, Hasta = hasta });
        var rawIngresos = await multi.ReadAsync<FechaTotalRaw>();
        var rawEgresos  = await multi.ReadAsync<FechaTotalRaw>();

        var mapIngresos = rawIngresos.ToDictionary(k => k.Fecha.ToString("dd MMM"), v => v.Total);
        var mapEgresos  = rawEgresos.ToDictionary(k  => k.Fecha.ToString("dd MMM"), v => v.Total);

        foreach (var f in dto.Fechas)
        {
            dto.Ingresos.Add(mapIngresos.TryGetValue(f, out var ing) ? ing : 0);
            dto.Egresos.Add(mapEgresos.TryGetValue(f,  out var egr) ? egr : 0);
        }

        return dto;
    }

    // ── sp_Dashboard_TopProductos ────────────────────────────────────────────
    public async Task<IEnumerable<DashboardTopProductoDto>> GetTopProductosAsync(int top = 5)
    {
        var desde = DateTime.Today.AddDays(-30);

        const string sql = @"
            SELECT TOP (@Top)
                a.nombre                           AS Producto,
                ISNULL(c.nombre, 'Sin Categoría') AS Categoria,
                SUM(dv.cantidad)                   AS CantidadVendida,
                SUM(dv.cantidad * dv.precio)       AS IngresoTotal
            FROM dbo.detalle_venta dv WITH (NOLOCK)
            INNER JOIN dbo.venta    v WITH (NOLOCK) ON dv.idventa    = v.idventa
            INNER JOIN dbo.articulo a WITH (NOLOCK) ON dv.idarticulo = a.idarticulo
            LEFT  JOIN dbo.categoria c WITH (NOLOCK) ON a.idcategoria = c.idcategoria
            WHERE v.fecha_hora >= @Desde AND v.estado != 'Anulado'
            GROUP BY a.nombre, c.nombre
            ORDER BY CantidadVendida DESC;";

        return await _db.QueryAsync<DashboardTopProductoDto>(sql, new { Top = top, Desde = desde });
    }

    // ── sp_Dashboard_TransaccionesSospechosas ────────────────────────────────
    public async Task<IEnumerable<DashboardTransaccionSospechosaDto>> GetTransaccionesSospechosasAsync(int dias = 7)
    {
        var desde = DateTime.Today.AddDays(-dias);

        const string sql = @"
            SELECT
                'Nota de Crédito'                   AS Tipo,
                ISNULL(p.nombre, 'Cliente General') AS Cliente,
                'Devolución por valor alto'         AS Motivo,
                nc.Total                            AS Monto,
                CASE WHEN nc.Total > 5000 THEN 'Alto'
                     WHEN nc.Total > 2000 THEN 'Medio'
                     ELSE 'Bajo' END                AS Riesgo,
                nc.FechaCreacion                    AS Fecha
            FROM dbo.NotaCredito nc WITH (NOLOCK)
            INNER JOIN dbo.venta   v WITH (NOLOCK) ON nc.IdVenta  = v.idventa
            LEFT  JOIN dbo.persona p WITH (NOLOCK) ON v.idcliente = p.idpersona
            WHERE nc.FechaCreacion >= @Desde AND nc.Total > 1000 AND nc.estado != 'Anulado'

            UNION ALL

            SELECT
                'Descuento Alto'                         AS Tipo,
                ISNULL(p.nombre, 'Cliente General')      AS Cliente,
                'Descuento > 20% del subtotal'           AS Motivo,
                SUM(dv.descuento)                        AS Monto,
                CASE WHEN SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.5 THEN 'Alto'
                     WHEN SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.3 THEN 'Medio'
                     ELSE 'Bajo' END                     AS Riesgo,
                v.fecha_hora                             AS Fecha
            FROM dbo.venta          v  WITH (NOLOCK)
            INNER JOIN dbo.detalle_venta dv WITH (NOLOCK) ON v.idventa   = dv.idventa
            LEFT  JOIN dbo.persona       p  WITH (NOLOCK) ON v.idcliente = p.idpersona
            WHERE v.fecha_hora >= @Desde AND v.estado != 'Anulado'
            GROUP BY v.idventa, p.nombre, v.fecha_hora
            HAVING SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.2

            UNION ALL

            SELECT
                'Nota de Crédito Tardía'             AS Tipo,
                ISNULL(p.nombre, 'Cliente General')  AS Cliente,
                CONCAT('NC creada ', DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion), ' días después de la venta') AS Motivo,
                nc.Total                             AS Monto,
                CASE WHEN DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 60 THEN 'Alto'
                     WHEN DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 30 THEN 'Medio'
                     ELSE 'Bajo' END                 AS Riesgo,
                nc.FechaCreacion                     AS Fecha
            FROM dbo.NotaCredito nc WITH (NOLOCK)
            INNER JOIN dbo.venta   v WITH (NOLOCK) ON nc.IdVenta  = v.idventa
            LEFT  JOIN dbo.persona p WITH (NOLOCK) ON v.idcliente = p.idpersona
            WHERE nc.FechaCreacion >= @Desde AND nc.estado != 'Anulado'
              AND DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 10

            ORDER BY Fecha DESC;";

        return await _db.QueryAsync<DashboardTransaccionSospechosaDto>(sql, new { Desde = desde });
    }

    // ── sp_Dashboard_Finanzas_Resumen ────────────────────────────────────────
    public async Task<DashboardFinanzasResumenDto> GetFinanzasResumenAsync()
    {
        const string sql = @"
            ;WITH SaldosCredito AS (
                SELECT
                    cr.Id_Credito,
                    cr.PrimerCredito,
                    MAX(ab.FechaDePago) AS UltimoAbono,
                    (
                        COALESCE((
                            SELECT SUM(v.total) FROM dbo.venta v
                            WHERE v.IdCredito = cr.Id_Credito AND v.estado != 'Anulado'
                        ), 0)
                        - COALESCE((
                            SELECT SUM(ab2.Monto) FROM dbo.abono ab2
                            WHERE ab2.Id_Credito = cr.Id_Credito AND ab2.Id_Estado = 0
                        ), 0)
                        - COALESCE((
                            SELECT SUM(nc.Total)
                            FROM dbo.NotaCredito nc
                            INNER JOIN dbo.venta v ON nc.IdVenta = v.idventa
                            WHERE v.IdCredito = cr.Id_Credito AND nc.estado != 'Anulado'
                        ), 0)
                    ) AS SaldoPendiente
                FROM dbo.credito cr
                LEFT JOIN dbo.abono ab ON ab.Id_Credito = cr.Id_Credito AND ab.Id_Estado = 0
                WHERE cr.Id_Estado = 1
                GROUP BY cr.Id_Credito, cr.PrimerCredito
            )
            SELECT
                ISNULL(SUM(SaldoPendiente), 0) AS TotalCartera,
                ISNULL(SUM(CASE
                    WHEN SaldoPendiente > 0 AND (
                        (UltimoAbono IS NOT NULL AND DATEDIFF(DAY, UltimoAbono, GETDATE()) > 15)
                        OR (UltimoAbono IS NULL  AND DATEDIFF(DAY, PrimerCredito, GETDATE()) > 15)
                    ) THEN SaldoPendiente * 0.15
                    ELSE 0
                END), 0) AS CobrosProyectados7d,
                ISNULL(COUNT(CASE
                    WHEN SaldoPendiente > 0 AND (
                        (UltimoAbono IS NOT NULL AND DATEDIFF(DAY, UltimoAbono, GETDATE()) > 30)
                        OR (UltimoAbono IS NULL  AND DATEDIFF(DAY, PrimerCredito, GETDATE()) > 30)
                    ) THEN 1
                    ELSE NULL
                END), 0) AS ClientesMorosos
            FROM SaldosCredito
            WHERE SaldoPendiente > 0;";

        var result = await _db.QueryFirstOrDefaultAsync<DashboardFinanzasResumenDto>(sql);
        return result ?? new DashboardFinanzasResumenDto();
    }

    private static decimal CalcularVariacion(decimal actual, decimal anterior)
    {
        if (anterior == 0) return actual > 0 ? 100 : 0;
        return Math.Round(((actual - anterior) / anterior) * 100, 2);
    }
}

// ── Private mapping helpers (file-scoped, invisible outside this file) ──────
file sealed class DashboardResumenRaw
{
    public decimal VentaHoy           { get; init; }
    public int     CantidadVentasHoy  { get; init; }
    public decimal VentaAyer          { get; init; }
    public int     CantidadVentasAyer { get; init; }
    public decimal UtilidadMes        { get; init; }
    public int     StockGlobal        { get; init; }
    public int     ArticulosActivos   { get; init; }
    public int     StockCritico       { get; init; }
    public decimal CuentasPorCobrar   { get; init; }
}

file sealed class FechaTotalRaw
{
    public DateTime Fecha { get; init; }
    public decimal  Total { get; init; }
}
