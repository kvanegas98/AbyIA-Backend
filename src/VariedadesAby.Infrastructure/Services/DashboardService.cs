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

    // ── Resumen por período ──────────────────────────────────────────────────
    public async Task<DashboardResumenDto> GetResumenAsync(DateTime? desde = null, DateTime? hasta = null)
    {
        var hoy    = DateTime.Today;
        // Período actual
        var periodoHasta  = (hasta ?? hoy).Date;
        var periodoDesde  = (desde ?? new DateTime(hoy.Year, hoy.Month, 1)).Date;
        var dias          = Math.Max((periodoHasta - periodoDesde).Days + 1, 1);
        // Período anterior equivalente (misma cantidad de días, justo antes)
        var anteriorHasta = periodoDesde.AddDays(-1);
        var anteriorDesde = anteriorHasta.AddDays(-(dias - 1));

        const string sql = @"
            SELECT
                -- Período actual
                ISNULL((SELECT SUM(total) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @PeriodoDesde AND fecha_hora < @PeriodoHasta
                           AND estado != 'Anulado'), 0) AS VentaPeriodo,
                ISNULL((SELECT COUNT(*) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @PeriodoDesde AND fecha_hora < @PeriodoHasta
                           AND estado != 'Anulado'), 0) AS CantidadVentasPeriodo,
                ISNULL((SELECT SUM(utilidad) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @PeriodoDesde AND fecha_hora < @PeriodoHasta
                           AND estado != 'Anulado'), 0) AS UtilidadPeriodo,
                -- Período anterior equivalente
                ISNULL((SELECT SUM(total) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @AnteriorDesde AND fecha_hora < @AnteriorHasta
                           AND estado != 'Anulado'), 0) AS VentaAnterior,
                ISNULL((SELECT COUNT(*) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @AnteriorDesde AND fecha_hora < @AnteriorHasta
                           AND estado != 'Anulado'), 0) AS CantidadVentasAnterior,
                ISNULL((SELECT SUM(utilidad) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @AnteriorDesde AND fecha_hora < @AnteriorHasta
                           AND estado != 'Anulado'), 0) AS UtilidadAnterior,
                -- Snapshot actual (ignoran el rango)
                ISNULL((SELECT SUM(stock) FROM dbo.sucursalArticulo WITH (NOLOCK)), 0) AS StockGlobal,
                ISNULL((SELECT COUNT(*) FROM dbo.articulo WITH (NOLOCK) WHERE condicion = 1), 0) AS ArticulosActivos,
                ISNULL((
                    SELECT COUNT(DISTINCT sa.idarticulo)
                    FROM dbo.sucursalArticulo sa WITH (NOLOCK)
                    INNER JOIN dbo.articulo a WITH (NOLOCK) ON sa.idarticulo = a.idarticulo
                    WHERE sa.stock <= 5 AND a.condicion = 1
                ), 0) AS StockCritico,
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
        p.Add("PeriodoDesde",  periodoDesde);
        p.Add("PeriodoHasta",  periodoHasta.AddDays(1));
        p.Add("AnteriorDesde", anteriorDesde);
        p.Add("AnteriorHasta", anteriorHasta.AddDays(1));

        var raw = await _db.QueryFirstOrDefaultAsync<DashboardResumenRaw>(sql, p);
        if (raw == null)
        {
            _logger.LogWarning("Dashboard resumen returned null");
            return new DashboardResumenDto();
        }

        decimal ticketPeriodo   = raw.CantidadVentasPeriodo   > 0 ? raw.VentaPeriodo   / raw.CantidadVentasPeriodo   : 0;
        decimal ticketAnterior  = raw.CantidadVentasAnterior  > 0 ? raw.VentaAnterior  / raw.CantidadVentasAnterior  : 0;

        return new DashboardResumenDto
        {
            VentaPeriodo                        = raw.VentaPeriodo,
            CantidadVentasPeriodo               = raw.CantidadVentasPeriodo,
            UtilidadPeriodo                     = raw.UtilidadPeriodo,
            TicketPeriodo                       = ticketPeriodo,
            CuentasPorCobrar                    = raw.CuentasPorCobrar,
            StockCritico                        = raw.StockCritico,
            StockGlobal                         = raw.StockGlobal,
            ArticulosActivos                    = raw.ArticulosActivos,
            PorcentajeVentavsPeriodoAnterior    = CalcularVariacion(raw.VentaPeriodo,    raw.VentaAnterior),
            PorcentajeUtilidadvsPeriodoAnterior = CalcularVariacion(raw.UtilidadPeriodo, raw.UtilidadAnterior),
            PorcentajeTicketvsPeriodoAnterior   = CalcularVariacion(ticketPeriodo,       ticketAnterior),
            FechaDesde                          = periodoDesde,
            FechaHasta                          = periodoHasta,
            DiasPeriodo                         = dias,
        };
    }

    // ── Ingresos vs Egresos por rango de fecha ───────────────────────────────
    public async Task<DashboardFinanzasDto> GetFinanzasAsync(DateTime desde, DateTime hasta)
    {
        var desdeDate = desde.Date;
        var hastaDate = hasta.Date.AddDays(1);
        var dias      = (hasta.Date - desde.Date).Days + 1;

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
        for (int i = 0; i < dias; i++)
            dto.Fechas.Add(desdeDate.AddDays(i).ToString("dd MMM"));

        using var multi = await _db.QueryMultipleAsync(sql, new { Desde = desdeDate, Hasta = hastaDate });
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

    // ── Top productos por rango de fecha ─────────────────────────────────────
    public async Task<IEnumerable<DashboardTopProductoDto>> GetTopProductosAsync(int top, DateTime desde, DateTime hasta)
    {
        const string sql = @"
            SELECT TOP (@Top)
                a.nombre                           AS Producto,
                ISNULL(c.nombre, 'Sin Categoría')  AS Categoria,
                SUM(dv.cantidad)                   AS CantidadVendida,
                SUM(dv.cantidad * dv.precio)        AS IngresoTotal
            FROM dbo.detalle_venta dv WITH (NOLOCK)
            INNER JOIN dbo.venta    v WITH (NOLOCK) ON dv.idventa    = v.idventa
            INNER JOIN dbo.articulo a WITH (NOLOCK) ON dv.idarticulo = a.idarticulo
            LEFT  JOIN dbo.categoria c WITH (NOLOCK) ON a.idcategoria = c.idcategoria
            WHERE v.fecha_hora >= @Desde AND v.fecha_hora < @Hasta
              AND v.estado != 'Anulado'
            GROUP BY a.nombre, c.nombre
            ORDER BY CantidadVendida DESC;";

        return await _db.QueryAsync<DashboardTopProductoDto>(sql, new
        {
            Top   = top,
            Desde = desde.Date,
            Hasta = hasta.Date.AddDays(1)
        });
    }

    // ── Top categorías por rango de fecha ────────────────────────────────────
    public async Task<IEnumerable<DashboardTopCategoriaDto>> GetTopCategoriasAsync(int top, DateTime desde, DateTime hasta)
    {
        const string sql = @"
            WITH CategoriasTotales AS (
                SELECT
                    ISNULL(c.nombre, 'Sin Categoría') AS Categoria,
                    SUM(dv.cantidad)                   AS CantidadVendida,
                    SUM(dv.cantidad * dv.precio)        AS IngresoTotal,
                    COUNT(DISTINCT v.idventa)           AS NumeroVentas
                FROM dbo.detalle_venta dv WITH (NOLOCK)
                INNER JOIN dbo.venta    v WITH (NOLOCK) ON dv.idventa    = v.idventa
                INNER JOIN dbo.articulo a WITH (NOLOCK) ON dv.idarticulo = a.idarticulo
                LEFT  JOIN dbo.categoria c WITH (NOLOCK) ON a.idcategoria = c.idcategoria
                WHERE v.fecha_hora >= @Desde AND v.fecha_hora < @Hasta
                  AND v.estado != 'Anulado'
                GROUP BY c.nombre
            ),
            GrandTotal AS (
                SELECT SUM(IngresoTotal) AS Total FROM CategoriasTotales
            )
            SELECT TOP (@Top)
                ct.Categoria,
                ct.CantidadVendida,
                ct.IngresoTotal,
                ct.NumeroVentas,
                CASE WHEN gt.Total > 0
                     THEN ROUND(ct.IngresoTotal * 100.0 / gt.Total, 2)
                     ELSE 0 END AS PorcentajeDelTotal
            FROM CategoriasTotales ct
            CROSS JOIN GrandTotal gt
            ORDER BY ct.IngresoTotal DESC;";

        return await _db.QueryAsync<DashboardTopCategoriaDto>(sql, new
        {
            Top   = top,
            Desde = desde.Date,
            Hasta = hasta.Date.AddDays(1)
        });
    }

    // ── Ventas por hora del día ───────────────────────────────────────────────
    public async Task<IEnumerable<DashboardVentasPorHoraDto>> GetVentasPorHoraAsync(DateTime fecha)
    {
        const string sql = @"
            SELECT
                DATEPART(HOUR, v.fecha_hora) AS Hora,
                SUM(v.total)                 AS Total,
                COUNT(*)                     AS Cantidad
            FROM dbo.venta v WITH (NOLOCK)
            WHERE CAST(v.fecha_hora AS DATE) = @Fecha
              AND v.estado != 'Anulado'
            GROUP BY DATEPART(HOUR, v.fecha_hora)
            ORDER BY Hora;";

        return await _db.QueryAsync<DashboardVentasPorHoraDto>(sql, new { Fecha = fecha.Date });
    }

    // ── Ventas por sucursal por rango de fecha ───────────────────────────────
    public async Task<IEnumerable<DashboardSucursalResumenDto>> GetVentasPorSucursalAsync(DateTime desde, DateTime hasta)
    {
        const string sql = @"
            SELECT
                ISNULL(s.nombre, 'Sin Sucursal')  AS Sucursal,
                ISNULL(SUM(v.total), 0)            AS VentaTotal,
                ISNULL(SUM(v.utilidad), 0)         AS UtilidadTotal,
                ISNULL(COUNT(v.idventa), 0)        AS TotalVentas,
                ISNULL(AVG(v.total), 0)            AS TicketPromedio
            FROM dbo.sucursal s WITH (NOLOCK)
            LEFT JOIN dbo.venta v WITH (NOLOCK)
                ON v.IdSucursal = s.idsucursal
               AND v.estado    != 'Anulado'
               AND v.fecha_hora >= @Desde
               AND v.fecha_hora <  @Hasta
            GROUP BY s.idsucursal, s.nombre
            ORDER BY VentaTotal DESC;";

        return await _db.QueryAsync<DashboardSucursalResumenDto>(sql, new
        {
            Desde = desde.Date,
            Hasta = hasta.Date.AddDays(1)
        });
    }

    // ── Aging de cartera (buckets de antigüedad) ─────────────────────────────
    public async Task<DashboardAgingCarteraDto> GetAgingCarteraAsync()
    {
        const string sql = @"
            ;WITH SaldoCreditos AS (
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
                    ) AS SaldoPendiente,
                    DATEDIFF(DAY,
                        ISNULL(MAX(ab.FechaDePago), cr.PrimerCredito),
                        GETDATE()) AS DiasAtraso
                FROM dbo.credito cr
                LEFT JOIN dbo.abono ab
                    ON ab.Id_Credito = cr.Id_Credito AND ab.Id_Estado = 0
                WHERE cr.Id_Estado = 1
                GROUP BY cr.Id_Credito, cr.PrimerCredito
            )
            SELECT
                ISNULL(SUM(CASE WHEN DiasAtraso <=  30 THEN SaldoPendiente ELSE 0 END), 0) AS Corriente,
                ISNULL(SUM(CASE WHEN DiasAtraso BETWEEN  31 AND 60 THEN SaldoPendiente ELSE 0 END), 0) AS Dias30a60,
                ISNULL(SUM(CASE WHEN DiasAtraso BETWEEN  61 AND 90 THEN SaldoPendiente ELSE 0 END), 0) AS Dias61a90,
                ISNULL(SUM(CASE WHEN DiasAtraso >   90 THEN SaldoPendiente ELSE 0 END), 0) AS MasDe90Dias,
                ISNULL(COUNT(CASE WHEN DiasAtraso <=  30 THEN 1 END), 0) AS ClientesCorriente,
                ISNULL(COUNT(CASE WHEN DiasAtraso BETWEEN  31 AND 60 THEN 1 END), 0) AS ClientesDias30a60,
                ISNULL(COUNT(CASE WHEN DiasAtraso BETWEEN  61 AND 90 THEN 1 END), 0) AS ClientesDias61a90,
                ISNULL(COUNT(CASE WHEN DiasAtraso >   90 THEN 1 END), 0) AS ClientesMasDe90Dias
            FROM SaldoCreditos
            WHERE SaldoPendiente > 0;";

        var result = await _db.QueryFirstOrDefaultAsync<DashboardAgingCarteraDto>(sql);
        return result ?? new DashboardAgingCarteraDto();
    }

    // ── Transacciones sospechosas por rango de fecha ─────────────────────────
    public async Task<IEnumerable<DashboardTransaccionSospechosaDto>> GetTransaccionesSospechosasAsync(DateTime desde, DateTime hasta)
    {
        const string sql = @"
            SELECT
                'Nota de Crédito'                   AS Tipo,
                ISNULL(p.nombre, 'Cliente General') AS Cliente,
                'Devolución por valor alto'          AS Motivo,
                nc.Total                             AS Monto,
                CASE WHEN nc.Total > 5000 THEN 'Alto'
                     WHEN nc.Total > 2000 THEN 'Medio'
                     ELSE 'Bajo' END                 AS Riesgo,
                nc.FechaCreacion                     AS Fecha,
                nc.Codigo                            AS NumeroNotaCredito,
                v.CodigoFactura                      AS NumeroFactura
            FROM dbo.NotaCredito nc WITH (NOLOCK)
            INNER JOIN dbo.venta   v WITH (NOLOCK) ON nc.IdVenta  = v.idventa
            LEFT  JOIN dbo.persona p WITH (NOLOCK) ON v.idcliente = p.idpersona
            WHERE nc.FechaCreacion >= @Desde AND nc.FechaCreacion < @Hasta
              AND nc.Total > 1000 AND nc.estado != 'Anulado'

            UNION ALL

            SELECT
                'Descuento Alto'                          AS Tipo,
                ISNULL(p.nombre, 'Cliente General')       AS Cliente,
                'Descuento > 20% del subtotal'            AS Motivo,
                SUM(dv.descuento)                         AS Monto,
                CASE WHEN SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.5 THEN 'Alto'
                     WHEN SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.3 THEN 'Medio'
                     ELSE 'Bajo' END                      AS Riesgo,
                v.fecha_hora                              AS Fecha,
                NULL                                      AS NumeroNotaCredito,
                v.CodigoFactura                           AS NumeroFactura
            FROM dbo.venta          v  WITH (NOLOCK)
            INNER JOIN dbo.detalle_venta dv WITH (NOLOCK) ON v.idventa   = dv.idventa
            LEFT  JOIN dbo.persona       p  WITH (NOLOCK) ON v.idcliente = p.idpersona
            WHERE v.fecha_hora >= @Desde AND v.fecha_hora < @Hasta
              AND v.estado != 'Anulado'
            GROUP BY v.idventa, p.nombre, v.fecha_hora, v.CodigoFactura
            HAVING SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.2

            UNION ALL

            SELECT
                'Nota de Crédito Tardía'              AS Tipo,
                ISNULL(p.nombre, 'Cliente General')   AS Cliente,
                CONCAT('NC creada ', DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion), ' días después de la venta') AS Motivo,
                nc.Total                              AS Monto,
                CASE WHEN DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 60 THEN 'Alto'
                     WHEN DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 30 THEN 'Medio'
                     ELSE 'Bajo' END                  AS Riesgo,
                nc.FechaCreacion                      AS Fecha,
                nc.Codigo                             AS NumeroNotaCredito,
                v.CodigoFactura                       AS NumeroFactura
            FROM dbo.NotaCredito nc WITH (NOLOCK)
            INNER JOIN dbo.venta   v WITH (NOLOCK) ON nc.IdVenta  = v.idventa
            LEFT  JOIN dbo.persona p WITH (NOLOCK) ON v.idcliente = p.idpersona
            WHERE nc.FechaCreacion >= @Desde AND nc.FechaCreacion < @Hasta
              AND nc.estado != 'Anulado'
              AND DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 10

            ORDER BY Fecha DESC;";

        return await _db.QueryAsync<DashboardTransaccionSospechosaDto>(sql, new
        {
            Desde = desde.Date,
            Hasta = hasta.Date.AddDays(1)
        });
    }

    // ── Resumen financiero de cartera ─────────────────────────────────────────
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

// ── Private mapping helpers ──────────────────────────────────────────────────
file sealed class DashboardResumenRaw
{
    public decimal VentaPeriodo            { get; init; }
    public int     CantidadVentasPeriodo   { get; init; }
    public decimal UtilidadPeriodo         { get; init; }
    public decimal VentaAnterior           { get; init; }
    public int     CantidadVentasAnterior  { get; init; }
    public decimal UtilidadAnterior        { get; init; }
    public int     StockGlobal             { get; init; }
    public int     ArticulosActivos        { get; init; }
    public int     StockCritico            { get; init; }
    public decimal CuentasPorCobrar        { get; init; }
}

file sealed class FechaTotalRaw
{
    public DateTime Fecha { get; init; }
    public decimal  Total { get; init; }
}
