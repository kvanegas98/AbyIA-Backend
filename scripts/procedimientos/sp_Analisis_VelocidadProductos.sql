-- =============================================
-- Consulta de referencia: Velocidad de Productos
-- Descripción: Productos con stock ordenados por unidades vendidas
--              en los últimos 30 días. Incluye margen y precio de compra.
-- Nota: La clasificación (Volando/Normal/Lento/Detenido), semáforo,
--       días para agotarse y recomendación se calculan en AnalisisService.
-- =============================================
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
)
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
        CASE WHEN a.precio_venta > 0 AND ISNULL(pc.precioPromedio, 0) > 0
        THEN (a.precio_venta - pc.precioPromedio) * 100.0 / a.precio_venta
        ELSE 0 END, 1)                                         AS margenPorcentaje
FROM dbo.articulo a WITH (NOLOCK)
LEFT JOIN dbo.categoria       cat WITH (NOLOCK) ON cat.idcategoria = a.idcategoria
LEFT JOIN StockPorArticulo    s                 ON s.idarticulo    = a.idarticulo
LEFT JOIN VentasUlt30         vu                ON vu.idarticulo   = a.idarticulo
LEFT JOIN PrecioCompra        pc                ON pc.idarticulo   = a.idarticulo
WHERE a.condicion = 1
  AND ISNULL(s.stockTotal, 0) > 0
ORDER BY ventasDiarias DESC, stockActual DESC;
