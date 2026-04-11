using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Analisis;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class AnalisisService : IAnalisisService
{
    private readonly IDbConnection _db;

    public AnalisisService(IDbConnection db)
    {
        _db = db;
    }

    // ─── Clientas de Oro ─────────────────────────────────────────────────────

    public async Task<ClientasOroResultadoDto> GetClientasOroAsync(FiltroAnalisisViewModel filtro)
    {
        filtro.pagina    = Math.Max(filtro.pagina, 1);
        filtro.porPagina = Math.Clamp(filtro.porPagina, 1, 100);

        var offset = (filtro.pagina - 1) * filtro.porPagina;
        var p      = new DynamicParameters();

        var whereCliente = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtro.busqueda))
        {
            whereCliente = "AND p.nombre LIKE @busqueda";
            p.Add("busqueda", $"%{filtro.busqueda.Trim()}%");
        }

        // ── Resumen KPI (sin paginar) ────────────────────────────────────────
        var sqlResumen = $@"
            SELECT
                COUNT(*)                                                    AS totalClientes,
                SUM(CASE WHEN totalCompras >= 8 THEN 1 ELSE 0 END)         AS clientesVip,
                SUM(CASE WHEN diasSinComprar > 60 THEN 1 ELSE 0 END)       AS enRiesgo,
                ROUND(AVG(CAST(ticketPromedio AS FLOAT)), 2)                AS ticketPromedioGeneral
            FROM (
                SELECT
                    p.idpersona,
                    COUNT(DISTINCT v.idventa)                               AS totalCompras,
                    AVG(v.total)                                            AS ticketPromedio,
                    DATEDIFF(DAY, MAX(v.fecha_hora), GETDATE())             AS diasSinComprar
                FROM dbo.persona p WITH (NOLOCK)
                INNER JOIN dbo.venta v WITH (NOLOCK)
                    ON v.idcliente = p.idpersona
                   AND v.estado   != 'Anulado'
                   AND v.idcliente NOT IN (13683, 13745, 13744)
                WHERE 1=1 {whereCliente}
                GROUP BY p.idpersona, p.nombre, p.telefono
                HAVING COUNT(DISTINCT v.idventa) >= 1
            ) base";

        // ── Count total (para paginación) ────────────────────────────────────
        var sqlCount = $@"
            SELECT COUNT(*)
            FROM (
                SELECT p.idpersona
                FROM dbo.persona p WITH (NOLOCK)
                INNER JOIN dbo.venta v WITH (NOLOCK)
                    ON v.idcliente = p.idpersona
                   AND v.estado   != 'Anulado'
                   AND v.idcliente NOT IN (13683, 13745, 13744)
                WHERE 1=1 {whereCliente}
                GROUP BY p.idpersona, p.nombre, p.telefono
                HAVING COUNT(DISTINCT v.idventa) >= 1
            ) x";

        // ── Data paginada ────────────────────────────────────────────────────
        var sqlData = $@"
            SELECT
                p.idpersona,
                p.nombre,
                ISNULL(p.telefono, '')                      AS telefono,
                COUNT(DISTINCT v.idventa)                   AS totalCompras,
                ROUND(SUM(v.total), 2)                      AS totalGastado,
                ROUND(AVG(v.total), 2)                      AS ticketPromedio,
                MAX(v.fecha_hora)                           AS ultimaCompra,
                DATEDIFF(DAY, MAX(v.fecha_hora), GETDATE()) AS diasSinComprar
            FROM dbo.persona p WITH (NOLOCK)
            INNER JOIN dbo.venta v WITH (NOLOCK)
                ON v.idcliente = p.idpersona
               AND v.estado   != 'Anulado'
               AND v.idcliente NOT IN (13683, 13745, 13744)
            WHERE 1=1 {whereCliente}
            GROUP BY p.idpersona, p.nombre, p.telefono
            HAVING COUNT(DISTINCT v.idventa) >= 1
            ORDER BY totalGastado DESC
            OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY";

        p.Add("offset",    offset);
        p.Add("porPagina", filtro.porPagina);

        var resumen = await _db.QueryFirstOrDefaultAsync<ClientasOroResumenDto>(sqlResumen, p)
                      ?? new ClientasOroResumenDto();
        var total   = await _db.ExecuteScalarAsync<int>(sqlCount, p);
        var raw     = await _db.QueryAsync<ClientaOroDto>(sqlData, p);

        foreach (var c in raw)
        {
            c.categoria = c.totalCompras switch
            {
                >= 8 => "VIP",
                >= 4 => "Frecuente",
                >= 2 => "Ocasional",
                _    => "Nuevo"
            };

            (c.semaforo, c.alerta) = c.diasSinComprar switch
            {
                <= 14 => ("verde",    "Activa recientemente"),
                <= 30 => ("amarillo", "Sin comprar hace más de 2 semanas"),
                <= 60 => ("naranja",  "Sin comprar hace más de 1 mes — considerar contactar"),
                _     => ("rojo",     "Sin comprar hace más de 2 meses — en riesgo de perderse")
            };
        }

        return new ClientasOroResultadoDto
        {
            resumen        = resumen,
            data           = raw,
            totalRegistros = total,
            pagina         = filtro.pagina,
            porPagina      = filtro.porPagina
        };
    }

    // ─── Velocidad de Productos ───────────────────────────────────────────────

    public async Task<VelocidadProductosResultadoDto> GetVelocidadProductosAsync(FiltroAnalisisViewModel filtro)
    {
        filtro.pagina    = Math.Max(filtro.pagina, 1);
        filtro.porPagina = Math.Clamp(filtro.porPagina, 1, 100);

        var offset = (filtro.pagina - 1) * filtro.porPagina;
        var p      = new DynamicParameters();

        var whereArticulo = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtro.busqueda))
        {
            whereArticulo = "AND a.nombre LIKE @busqueda";
            p.Add("busqueda", $"%{filtro.busqueda.Trim()}%");
        }

        var ctes = @"
            WITH StockPorArticulo AS (
                SELECT idarticulo, SUM(ISNULL(stock, 0)) AS stockTotal
                FROM dbo.sucursalArticulo WITH (NOLOCK)
                GROUP BY idarticulo
            ),
            VentasUlt30 AS (
                SELECT dv.idarticulo, SUM(dv.cantidad) AS vendidosUlt30d
                FROM dbo.detalle_venta dv WITH (NOLOCK)
                INNER JOIN dbo.venta v WITH (NOLOCK)
                    ON v.idventa    = dv.idventa
                   AND v.estado    != 'Anulado'
                   AND v.fecha_hora >= DATEADD(DAY, -30, GETDATE())
                GROUP BY dv.idarticulo
            ),
            PrecioCompra AS (
                SELECT di.idarticulo, AVG(di.precio) AS precioPromedio
                FROM dbo.detalle_ingreso di WITH (NOLOCK)
                INNER JOIN dbo.ingreso i WITH (NOLOCK)
                    ON i.idingreso = di.idingreso
                   AND i.estado   != 'ANULADO'
                GROUP BY di.idarticulo
            )";

        var joins = @"
            FROM dbo.articulo a WITH (NOLOCK)
            LEFT JOIN dbo.categoria    cat WITH (NOLOCK) ON cat.idcategoria = a.idcategoria
            LEFT JOIN StockPorArticulo s                 ON s.idarticulo    = a.idarticulo
            LEFT JOIN VentasUlt30      vu                ON vu.idarticulo   = a.idarticulo
            LEFT JOIN PrecioCompra     pc                ON pc.idarticulo   = a.idarticulo";

        var baseWhere = $"WHERE a.condicion = 1 AND ISNULL(s.stockTotal, 0) > 0 {whereArticulo}";

        // ── Resumen KPI ──────────────────────────────────────────────────────
        var sqlResumen = $@"
            {ctes}
            SELECT
                COUNT(*)                                                            AS totalProductos,
                SUM(CASE WHEN ISNULL(vu.vendidosUlt30d,0) >= 30 THEN 1 ELSE 0 END) AS volando,
                SUM(CASE WHEN ISNULL(vu.vendidosUlt30d,0) = 0  THEN 1 ELSE 0 END)  AS detenidos,
                SUM(CASE
                    WHEN ISNULL(vu.vendidosUlt30d,0) > 0
                     AND ISNULL(s.stockTotal,0) / (ISNULL(vu.vendidosUlt30d,0) / 30.0) <= 7
                    THEN 1 ELSE 0 END)                                              AS quiebreInminente,
                ROUND(AVG(
                    CASE WHEN a.precio_venta > 0 AND ISNULL(pc.precioPromedio,0) > 0
                    THEN (a.precio_venta - pc.precioPromedio) * 100.0 / a.precio_venta
                    ELSE NULL END), 1)                                              AS margenPromedio
            {joins}
            {baseWhere}";

        // ── Count total ──────────────────────────────────────────────────────
        var sqlCount = $@"
            {ctes}
            SELECT COUNT(*)
            {joins}
            {baseWhere}";

        // ── Data paginada ────────────────────────────────────────────────────
        var sqlData = $@"
            {ctes}
            SELECT
                a.codigo,
                a.nombre                                                    AS articulo,
                ISNULL(cat.nombre, 'Sin categoría')                        AS categoria,
                ISNULL(s.stockTotal, 0)                                    AS stockActual,
                ISNULL(vu.vendidosUlt30d, 0)                               AS vendidosUlt30d,
                ROUND(ISNULL(vu.vendidosUlt30d, 0) / 30.0, 2)             AS ventasDiarias,
                a.precio_venta                                              AS precioVenta,
                ISNULL(pc.precioPromedio, 0)                               AS precioCompra,
                ROUND(
                    CASE WHEN a.precio_venta > 0 AND ISNULL(pc.precioPromedio,0) > 0
                    THEN (a.precio_venta - pc.precioPromedio) * 100.0 / a.precio_venta
                    ELSE 0 END, 1)                                         AS margenPorcentaje
            {joins}
            {baseWhere}
            ORDER BY vendidosUlt30d DESC, stockActual DESC
            OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY";

        p.Add("offset",    offset);
        p.Add("porPagina", filtro.porPagina);

        var resumen = await _db.QueryFirstOrDefaultAsync<VelocidadResumenDto>(sqlResumen, p)
                      ?? new VelocidadResumenDto();
        var total   = await _db.ExecuteScalarAsync<int>(sqlCount, p);
        var raw     = await _db.QueryAsync<VelocidadProductoDto>(sqlData, p);

        foreach (var item in raw)
        {
            item.diasParaAgotarse = item.ventasDiarias > 0
                ? (int?)Math.Ceiling(item.stockActual / (double)item.ventasDiarias)
                : null;

            (item.velocidad, item.semaforo, item.recomendacion) = item.vendidosUlt30d switch
            {
                >= 30 => ("Volando",  "verde",
                          "Alta rotación — Reabastecer pronto para no perder ventas."),
                >= 10 => ("Normal",   "amarillo",
                          "Rotación normal — Monitorear stock para el próximo pedido."),
                >= 1  => ("Lento",    "naranja",
                          "Baja rotación — Evaluar promoción o precio antes del próximo ingreso."),
                _     => ("Detenido", "rojo",
                          "Sin ventas en 30 días — Revisar precio, visibilidad o hacer promoción urgente.")
            };
        }

        return new VelocidadProductosResultadoDto
        {
            resumen        = resumen,
            data           = raw,
            totalRegistros = total,
            pagina         = filtro.pagina,
            porPagina      = filtro.porPagina
        };
    }

    // ─── Rendimiento de Proveedores ───────────────────────────────────────────

    public async Task<RendimientoProveedoresResultadoDto> GetRendimientoProveedoresAsync(FiltroAnalisisViewModel filtro)
    {
        filtro.pagina    = Math.Max(filtro.pagina, 1);
        filtro.porPagina = Math.Clamp(filtro.porPagina, 1, 100);

        var offset = (filtro.pagina - 1) * filtro.porPagina;
        var p      = new DynamicParameters();

        var whereProveedor = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtro.busqueda))
        {
            whereProveedor = "AND p.nombre LIKE @busqueda";
            p.Add("busqueda", $"%{filtro.busqueda.Trim()}%");
        }

        var ctes = @"
            WITH ComprasPorProveedor AS (
                SELECT
                    i.idproveedor,
                    di.idarticulo,
                    SUM(di.cantidad)             AS totalComprado,
                    SUM(di.cantidad * di.precio) AS costoTotal,
                    AVG(di.precio)               AS precioCompraPromedio
                FROM dbo.detalle_ingreso di WITH (NOLOCK)
                INNER JOIN dbo.ingreso i WITH (NOLOCK)
                    ON i.idingreso = di.idingreso
                   AND i.estado   != 'ANULADO'
                GROUP BY i.idproveedor, di.idarticulo
            ),
            VentasPorArticulo AS (
                SELECT
                    dv.idarticulo,
                    SUM(dv.cantidad)                                        AS totalVendido,
                    SUM(dv.cantidad * dv.precio - ISNULL(dv.descuento, 0)) AS ingresoVentas
                FROM dbo.detalle_venta dv WITH (NOLOCK)
                INNER JOIN dbo.venta v WITH (NOLOCK)
                    ON v.idventa = dv.idventa
                   AND v.estado != 'Anulado'
                GROUP BY dv.idarticulo
            ),
            StockPorArticulo AS (
                SELECT idarticulo, SUM(ISNULL(stock, 0)) AS stockTotal
                FROM dbo.sucursalArticulo WITH (NOLOCK)
                GROUP BY idarticulo
            ),
            DiasRecuperacion AS (
                SELECT
                    i.idproveedor,
                    AVG(DATEDIFF(DAY, i.fecha_hora, v.fecha_hora)) AS diasPromedio
                FROM dbo.ingreso i WITH (NOLOCK)
                INNER JOIN dbo.detalle_ingreso di WITH (NOLOCK) ON di.idingreso  = i.idingreso
                INNER JOIN dbo.detalle_venta   dv WITH (NOLOCK) ON dv.idarticulo = di.idarticulo
                INNER JOIN dbo.venta           v  WITH (NOLOCK) ON v.idventa     = dv.idventa
                                                                AND v.estado    != 'Anulado'
                                                                AND v.fecha_hora >= i.fecha_hora
                WHERE i.estado != 'ANULADO'
                GROUP BY i.idproveedor
            ),
            BaseProveedor AS (
                SELECT
                    p.idpersona                                                         AS idproveedor,
                    p.nombre                                                            AS proveedor,
                    COUNT(DISTINCT c.idarticulo)                                        AS totalProductos,
                    ROUND(SUM(c.costoTotal), 2)                                         AS totalInvertido,
                    ROUND(SUM(ISNULL(va.totalVendido,0) * c.precioCompraPromedio), 2)  AS costoVendido,
                    ROUND(SUM(ISNULL(s.stockTotal,0) * c.precioCompraPromedio), 2)     AS valorStockActual,
                    ROUND(SUM(ISNULL(va.ingresoVentas,0)), 2)                          AS ingresoReal,
                    ROUND(SUM(ISNULL(va.ingresoVentas,0))
                          - SUM(ISNULL(va.totalVendido,0) * c.precioCompraPromedio), 2) AS margenBruto,
                    ROUND(CASE WHEN SUM(ISNULL(va.ingresoVentas,0)) > 0
                          THEN (SUM(ISNULL(va.ingresoVentas,0))
                               - SUM(ISNULL(va.totalVendido,0) * c.precioCompraPromedio))
                               * 100.0 / SUM(ISNULL(va.ingresoVentas,0))
                          ELSE 0 END, 1)                                               AS margenPorcentaje,
                    ROUND(CASE WHEN SUM(c.costoTotal) > 0
                          THEN (SUM(ISNULL(va.ingresoVentas,0))
                               - SUM(ISNULL(va.totalVendido,0) * c.precioCompraPromedio))
                               * 100.0 / SUM(c.costoTotal)
                          ELSE 0 END, 1)                                               AS roi,
                    ROUND(CASE WHEN SUM(c.totalComprado) > 0
                          THEN SUM(ISNULL(va.totalVendido,0)) * 100.0 / SUM(c.totalComprado)
                          ELSE 0 END, 1)                                               AS porcentajeRotacion,
                    ISNULL(dr.diasPromedio, 0)                                         AS diasRecuperacionPromedio,
                    MAX(i.fecha_hora)                                                   AS ultimaCompra
                FROM dbo.persona p WITH (NOLOCK)
                INNER JOIN ComprasPorProveedor c ON c.idproveedor  = p.idpersona
                INNER JOIN dbo.ingreso i WITH (NOLOCK)
                    ON i.idproveedor = p.idpersona
                   AND i.estado     != 'ANULADO'
                LEFT JOIN VentasPorArticulo va ON va.idarticulo  = c.idarticulo
                LEFT JOIN StockPorArticulo  s  ON s.idarticulo   = c.idarticulo
                LEFT JOIN DiasRecuperacion  dr ON dr.idproveedor = p.idpersona
                WHERE p.idpersona != 13686 " + whereProveedor + @"
                GROUP BY p.idpersona, p.nombre, dr.diasPromedio
            )";

        // ── Resumen KPI ──────────────────────────────────────────────────────
        var sqlResumen = $@"
            {ctes}
            SELECT
                COUNT(*)                                                AS totalProveedores,
                SUM(CASE WHEN roi >= 30 AND margenPorcentaje >= 25
                         THEN 1 ELSE 0 END)                            AS proveedoresEstrella,
                ROUND(SUM(totalInvertido), 2)                          AS totalInvertido,
                ROUND(SUM(ingresoReal), 2)                             AS ingresoTotal,
                ROUND(AVG(CAST(roi AS FLOAT)), 1)                      AS roiPromedio
            FROM BaseProveedor";

        // ── Count total ──────────────────────────────────────────────────────
        var sqlCount = $@"
            {ctes}
            SELECT COUNT(*) FROM BaseProveedor";

        // ── Data paginada ────────────────────────────────────────────────────
        var sqlData = $@"
            {ctes}
            SELECT * FROM BaseProveedor
            ORDER BY roi DESC
            OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY";

        p.Add("offset",    offset);
        p.Add("porPagina", filtro.porPagina);

        var resumen = await _db.QueryFirstOrDefaultAsync<RendimientoResumenDto>(sqlResumen, p)
                      ?? new RendimientoResumenDto();
        var total   = await _db.ExecuteScalarAsync<int>(sqlCount, p);
        var raw     = await _db.QueryAsync<RendimientoProveedorDto>(sqlData, p);

        foreach (var r in raw)
        {
            (r.clasificacion, r.semaforo, r.recomendacion) = (r.roi, r.margenPorcentaje, r.porcentajeRotacion) switch
            {
                ( >= 30, >= 25, _)    => ("Estrella", "verde",
                                          "Excelente proveedor — Alta rentabilidad y buena rotación. Priorizar en próximos pedidos."),
                ( >= 15, >= 15, _)    => ("Rentable",  "amarillo",
                                          "Buen proveedor — Margen sólido. Evaluar aumentar volumen de compra."),
                ( >= 0,  _,    >= 40) => ("Regular",   "naranja",
                                          "Rotación aceptable pero margen bajo — Negociar mejores precios de compra."),
                ( < 0,   _,    _)     => ("Revisar",   "rojo",
                                          "Inversión sin retorno aún — Reducir pedidos hasta mejorar la rotación."),
                _                     => ("Regular",   "naranja",
                                          "Rendimiento medio — Monitorear evolución antes del próximo pedido.")
            };
        }

        return new RendimientoProveedoresResultadoDto
        {
            resumen        = resumen,
            data           = raw,
            totalRegistros = total,
            pagina         = filtro.pagina,
            porPagina      = filtro.porPagina
        };
    }
}
