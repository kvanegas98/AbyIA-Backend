-- =============================================
-- Consulta de referencia: Rendimiento de Proveedores
-- Descripción: Cruza precio de compra (ingreso) con precio de venta real
--              para calcular margen bruto y ROI por proveedor.
-- Nota: La clasificación (Estrella/Rentable/Regular/Revisar), semáforo
--       y recomendación se calculan en la capa de aplicación (AnalisisService).
-- =============================================
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
        -- Ingreso real: precio de venta × cantidad menos descuento por línea
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
    -- Promedio de días entre fecha de compra al proveedor y venta al cliente
    -- Solo cuenta ventas posteriores a la compra para evitar ruido
    SELECT
        i.idproveedor,
        AVG(DATEDIFF(DAY, i.fecha_hora, v.fecha_hora)) AS diasPromedio
    FROM dbo.ingreso i WITH (NOLOCK)
    INNER JOIN dbo.detalle_ingreso di WITH (NOLOCK) ON di.idingreso   = i.idingreso
    INNER JOIN dbo.detalle_venta   dv WITH (NOLOCK) ON dv.idarticulo  = di.idarticulo
    INNER JOIN dbo.venta           v  WITH (NOLOCK) ON v.idventa      = dv.idventa
                                                    AND v.estado      != 'Anulado'
                                                    AND v.fecha_hora  >= i.fecha_hora
    WHERE i.estado != 'ANULADO'
    GROUP BY i.idproveedor
)
SELECT
    p.idpersona                                                         AS idproveedor,
    p.nombre                                                            AS proveedor,
    COUNT(DISTINCT c.idarticulo)                                        AS totalProductos,
    ROUND(SUM(c.costoTotal), 2)                                         AS totalInvertido,
    ROUND(SUM(ISNULL(va.totalVendido, 0) * c.precioCompraPromedio), 2) AS costoVendido,
    ROUND(SUM(ISNULL(s.stockTotal, 0)  * c.precioCompraPromedio), 2)   AS valorStockActual,
    ROUND(SUM(ISNULL(va.ingresoVentas, 0)), 2)                         AS ingresoReal,
    -- Margen bruto = ingresos reales − costo de lo vendido
    ROUND(
        SUM(ISNULL(va.ingresoVentas, 0))
        - SUM(ISNULL(va.totalVendido, 0) * c.precioCompraPromedio), 2) AS margenBruto,
    -- % Margen sobre ventas
    ROUND(
        CASE WHEN SUM(ISNULL(va.ingresoVentas, 0)) > 0
        THEN (SUM(ISNULL(va.ingresoVentas, 0))
              - SUM(ISNULL(va.totalVendido, 0) * c.precioCompraPromedio))
             * 100.0 / SUM(ISNULL(va.ingresoVentas, 0))
        ELSE 0 END, 1)                                                  AS margenPorcentaje,
    -- ROI = margen bruto / total invertido × 100
    ROUND(
        CASE WHEN SUM(c.costoTotal) > 0
        THEN (SUM(ISNULL(va.ingresoVentas, 0))
              - SUM(ISNULL(va.totalVendido, 0) * c.precioCompraPromedio))
             * 100.0 / SUM(c.costoTotal)
        ELSE 0 END, 1)                                                  AS roi,
    -- % Rotación = unidades vendidas / unidades compradas
    ROUND(
        CASE WHEN SUM(c.totalComprado) > 0
        THEN SUM(ISNULL(va.totalVendido, 0)) * 100.0 / SUM(c.totalComprado)
        ELSE 0 END, 1)                                                  AS porcentajeRotacion,
    ISNULL(dr.diasPromedio, 0)                                          AS diasRecuperacionPromedio,
    MAX(i.fecha_hora)                                                   AS ultimaCompra
FROM dbo.persona p WITH (NOLOCK)
INNER JOIN ComprasPorProveedor c ON c.idproveedor  = p.idpersona
INNER JOIN dbo.ingreso i WITH (NOLOCK)
    ON i.idproveedor = p.idpersona
   AND i.estado     != 'ANULADO'
LEFT JOIN VentasPorArticulo  va ON va.idarticulo  = c.idarticulo
LEFT JOIN StockPorArticulo   s  ON s.idarticulo   = c.idarticulo
LEFT JOIN DiasRecuperacion   dr ON dr.idproveedor = p.idpersona
GROUP BY p.idpersona, p.nombre, dr.diasPromedio
ORDER BY roi DESC;
