using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Sucursal;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class SucursalService : ISucursalService
{
    private readonly IDbConnection _db;

    public SucursalService(IDbConnection db)
    {
        _db = db;
    }

    // ─── 1. Comparativo de Rendimiento ───────────────────────────────────────

    public async Task<IEnumerable<RendimientoSucursalDto>> GetRendimientoAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        var dias              = (fechaHasta.Date - fechaDesde.Date).Days;
        var fechaInicioAnterior = fechaDesde.Date.AddDays(-dias);

        const string sql = @"
            WITH VentasPorSucursal AS (
                SELECT
                    v.IdSucursal,
                    COUNT(CASE WHEN v.fecha_hora >= @fechaDesde AND v.fecha_hora <= @fechaHasta
                               THEN v.idventa END)                                   AS totalVentas,
                    ROUND(SUM(CASE WHEN v.fecha_hora >= @fechaDesde AND v.fecha_hora <= @fechaHasta
                                   THEN v.total ELSE 0 END), 2)                      AS ventaTotal,
                    ROUND(SUM(CASE WHEN v.fecha_hora >= @fechaDesde AND v.fecha_hora <= @fechaHasta
                                   THEN v.utilidad ELSE 0 END), 2)                   AS utilidadTotal,
                    ROUND(AVG(CASE WHEN v.fecha_hora >= @fechaDesde AND v.fecha_hora <= @fechaHasta
                                   THEN v.total END), 2)                             AS ticketPromedio,
                    ROUND(SUM(CASE WHEN v.fecha_hora >= @fechaInicioAnterior
                                    AND v.fecha_hora <  @fechaDesde
                                   THEN v.total ELSE 0 END), 2)                      AS ventaAnterior
                FROM dbo.venta v WITH (NOLOCK)
                WHERE v.estado     != 'Anulado'
                  AND v.fecha_hora >= @fechaInicioAnterior
                  AND v.fecha_hora <= @fechaHasta
                GROUP BY v.IdSucursal
            ),
            UnidadesPorSucursal AS (
                SELECT
                    v.IdSucursal,
                    SUM(dv.cantidad) AS unidadesVendidas
                FROM dbo.venta v WITH (NOLOCK)
                INNER JOIN dbo.detalle_venta dv WITH (NOLOCK) ON dv.idventa = v.idventa
                WHERE v.estado     != 'Anulado'
                  AND v.fecha_hora >= @fechaDesde
                  AND v.fecha_hora <= @fechaHasta
                GROUP BY v.IdSucursal
            )
            SELECT
                s.idsucursal,
                s.nombre                          AS sucursal,
                ISNULL(vps.totalVentas, 0)        AS totalVentas,
                ISNULL(vps.ventaTotal, 0)         AS ventaTotal,
                ISNULL(vps.utilidadTotal, 0)      AS utilidadTotal,
                ISNULL(vps.ticketPromedio, 0)     AS ticketPromedio,
                ISNULL(ups.unidadesVendidas, 0)   AS unidadesVendidas,
                ISNULL(vps.ventaAnterior, 0)      AS ventaAnterior
            FROM dbo.sucursal s WITH (NOLOCK)
            LEFT JOIN VentasPorSucursal   vps ON vps.IdSucursal = s.idsucursal
            LEFT JOIN UnidadesPorSucursal ups ON ups.IdSucursal = s.idsucursal
            ORDER BY ventaTotal DESC";

        var raw = await _db.QueryAsync<RendimientoSucursalDto>(sql, new
        {
            fechaDesde          = fechaDesde.Date,
            fechaHasta          = fechaHasta.Date.AddDays(1).AddSeconds(-1),
            fechaInicioAnterior
        });

        foreach (var r in raw)
        {
            r.fechaInicioActual   = fechaDesde.Date;
            r.fechaFinActual      = fechaHasta.Date;
            r.fechaInicioAnterior = fechaInicioAnterior;
            r.fechaFinAnterior    = fechaDesde.Date.AddDays(-1);

            r.variacion = r.ventaAnterior > 0
                ? Math.Round((r.ventaTotal - r.ventaAnterior) / r.ventaAnterior * 100, 1)
                : 0;

            (r.tendencia, r.semaforo) = r.variacion switch
            {
                > 5  => ("sube",    "verde"),
                < -5 => ("baja",    "rojo"),
                _    => ("estable", "gris")
            };
        }

        return raw;
    }

    // ─── 2. Tendencia diaria ─────────────────────────────────────────────────

    public async Task<IEnumerable<TendenciaSucursalDto>> GetTendenciaAsync(
        DateTime fechaDesde, DateTime fechaHasta)
    {
        const string sql = @"
            SELECT
                s.idsucursal,
                s.nombre                    AS sucursal,
                CAST(v.fecha_hora AS DATE)  AS fecha,
                ROUND(SUM(v.total), 2)      AS ventaDia
            FROM dbo.sucursal s WITH (NOLOCK)
            INNER JOIN dbo.venta v WITH (NOLOCK)
                ON v.IdSucursal  = s.idsucursal
               AND v.estado     != 'Anulado'
               AND v.fecha_hora >= @fechaDesde
               AND v.fecha_hora <= @fechaHasta
            GROUP BY s.idsucursal, s.nombre, CAST(v.fecha_hora AS DATE)
            ORDER BY s.nombre, fecha";

        return await _db.QueryAsync<TendenciaSucursalDto>(sql, new
        {
            fechaDesde = fechaDesde.Date,
            fechaHasta = fechaHasta.Date.AddDays(1).AddSeconds(-1)
        });
    }

    // ─── 3. Inventario valorizado por sucursal ───────────────────────────────

    public async Task<InventarioValorizadoResultadoDto> GetInventarioValorizadoAsync()
    {
        const string sql = @"
            SELECT
                s.idsucursal,
                s.nombre                                                        AS sucursal,
                COUNT(DISTINCT sa.idarticulo)                                   AS articulos,
                SUM(sa.stock)                                                   AS totalUnidades,
                ROUND(SUM(sa.stock * a.precio_compra), 2)                       AS valorCosto,
                ROUND(SUM(sa.stock * a.precio_venta),  2)                       AS valorVenta,
                ROUND(SUM(sa.stock * (a.precio_venta - a.precio_compra)), 2)    AS gananciasPotenciales
            FROM dbo.sucursal           s   WITH (NOLOCK)
            JOIN dbo.sucursalArticulo   sa  WITH (NOLOCK) ON sa.idsucursal  = s.idsucursal
            JOIN dbo.articulo           a   WITH (NOLOCK) ON a.idarticulo   = sa.idarticulo
            WHERE a.condicion = 1
              AND sa.stock    > 0
            GROUP BY s.idsucursal, s.nombre
            ORDER BY valorCosto DESC";

        var filas = (await _db.QueryAsync<InventarioSucursalDto>(sql)).ToList();

        var totalCosto = filas.Sum(f => f.valorCosto);

        foreach (var f in filas)
        {
            f.margenPotencial    = f.valorCosto > 0
                ? Math.Round(f.gananciasPotenciales / f.valorCosto * 100, 1)
                : 0;
            f.porcentajeDelTotal = totalCosto > 0
                ? Math.Round(f.valorCosto / totalCosto * 100, 1)
                : 0;
        }

        var resumen = new InventarioValorizadoResumenDto
        {
            totalSucursales         = filas.Count,
            totalArticulosDistintos = filas.Sum(f => f.articulos),
            totalUnidades           = filas.Sum(f => f.totalUnidades),
            valorTotalCosto         = Math.Round(totalCosto, 2),
            valorTotalVenta         = Math.Round(filas.Sum(f => f.valorVenta), 2),
            gananciasPotenciales    = Math.Round(filas.Sum(f => f.gananciasPotenciales), 2),
            margenPotencial         = totalCosto > 0
                ? Math.Round(filas.Sum(f => f.gananciasPotenciales) / totalCosto * 100, 1)
                : 0
        };

        return new InventarioValorizadoResultadoDto
        {
            resumen    = resumen,
            sucursales = filas
        };
    }

    // ─── 4. Inventario valorizado por categoría ──────────────────────────────

    public async Task<IEnumerable<InventarioCategoriaDto>> GetInventarioCategoriaAsync()
    {
        const string sql = @"
            SELECT
                ISNULL(cat.nombre, 'Sin categoría')                             AS categoria,
                COUNT(DISTINCT sa.idarticulo)                                   AS articulos,
                SUM(sa.stock)                                                   AS totalUnidades,
                ROUND(SUM(sa.stock * a.precio_compra), 2)                       AS valorCosto,
                ROUND(SUM(sa.stock * a.precio_venta),  2)                       AS valorVenta,
                ROUND(SUM(sa.stock * (a.precio_venta - a.precio_compra)), 2)    AS gananciasPotenciales
            FROM dbo.sucursalArticulo   sa  WITH (NOLOCK)
            JOIN dbo.articulo           a   WITH (NOLOCK) ON a.idarticulo    = sa.idarticulo
            LEFT JOIN dbo.categoria     cat WITH (NOLOCK) ON cat.idcategoria = a.idcategoria
            WHERE a.condicion = 1
              AND sa.stock    > 0
            GROUP BY cat.nombre
            ORDER BY valorCosto DESC";

        var filas = (await _db.QueryAsync<InventarioCategoriaDto>(sql)).ToList();

        var totalCosto = filas.Sum(f => f.valorCosto);

        foreach (var f in filas)
        {
            f.margenPotencial    = f.valorCosto > 0
                ? Math.Round(f.gananciasPotenciales / f.valorCosto * 100, 1)
                : 0;
            f.porcentajeDelTotal = totalCosto > 0
                ? Math.Round(f.valorCosto / totalCosto * 100, 1)
                : 0;
        }

        return filas;
    }

    // ─── 5. Top 10 capital inmovilizado ──────────────────────────────────────

    public async Task<IEnumerable<TopCapitalInmovilizadoDto>> GetTopCapitalInmovilizadoAsync()
    {
        const string sql = @"
            SELECT TOP 10
                a.codigo,
                a.nombre                                        AS articulo,
                ISNULL(cat.nombre, 'Sin categoría')            AS categoria,
                SUM(sa.stock)                                   AS stockTotal,
                a.precio_compra                                 AS precioCompra,
                ROUND(SUM(sa.stock) * a.precio_compra, 2)       AS valorCosto,
                DATEDIFF(DAY,
                    MAX(v.fecha_hora),
                    GETDATE())                                  AS diasSinVenta
            FROM dbo.sucursalArticulo   sa  WITH (NOLOCK)
            JOIN dbo.articulo           a   WITH (NOLOCK) ON a.idarticulo    = sa.idarticulo
            LEFT JOIN dbo.categoria     cat WITH (NOLOCK) ON cat.idcategoria = a.idcategoria
            LEFT JOIN dbo.detalle_venta dv  WITH (NOLOCK) ON dv.idarticulo   = a.idarticulo
            LEFT JOIN dbo.venta         v   WITH (NOLOCK) ON v.idventa       = dv.idventa
                                                          AND v.estado       != 'Anulado'
            WHERE a.condicion = 1
              AND sa.stock    > 0
            GROUP BY a.idarticulo, a.codigo, a.nombre, cat.nombre, a.precio_compra
            ORDER BY valorCosto DESC";

        return await _db.QueryAsync<TopCapitalInmovilizadoDto>(sql);
    }

    // ─── 6. Top productos por sucursal ────────────────────────────────────────

    public async Task<IEnumerable<TopProductoSucursalDto>> GetTopProductosAsync(
        DateTime fechaDesde, DateTime fechaHasta, int top = 5)
    {
        const string sql = @"
            WITH VentasPorProductoSucursal AS (
                SELECT
                    s.idsucursal,
                    s.nombre                                                          AS sucursal,
                    a.codigo,
                    a.nombre                                                          AS articulo,
                    ISNULL(cat.nombre, 'Sin categoría')                              AS categoria,
                    SUM(dv.cantidad)                                                  AS unidadesVendidas,
                    ROUND(SUM(dv.cantidad * dv.precio - ISNULL(dv.descuento, 0)), 2) AS ingresoGenerado
                FROM dbo.sucursal s WITH (NOLOCK)
                INNER JOIN dbo.venta v WITH (NOLOCK)
                    ON v.IdSucursal  = s.idsucursal
                   AND v.estado     != 'Anulado'
                   AND v.fecha_hora >= @fechaDesde
                   AND v.fecha_hora <= @fechaHasta
                INNER JOIN dbo.detalle_venta dv WITH (NOLOCK) ON dv.idventa    = v.idventa
                INNER JOIN dbo.articulo      a  WITH (NOLOCK) ON a.idarticulo  = dv.idarticulo
                LEFT  JOIN dbo.categoria     cat WITH (NOLOCK) ON cat.idcategoria = a.idcategoria
                GROUP BY s.idsucursal, s.nombre, a.idarticulo, a.codigo, a.nombre, cat.nombre
            ),
            Ranked AS (
                SELECT *,
                    ROW_NUMBER() OVER (
                        PARTITION BY idsucursal
                        ORDER BY unidadesVendidas DESC
                    ) AS ranking
                FROM VentasPorProductoSucursal
            )
            SELECT * FROM Ranked
            WHERE ranking <= @top
            ORDER BY sucursal, ranking";

        return await _db.QueryAsync<TopProductoSucursalDto>(sql, new
        {
            fechaDesde = fechaDesde.Date,
            fechaHasta = fechaHasta.Date.AddDays(1).AddSeconds(-1),
            top
        });
    }
}
