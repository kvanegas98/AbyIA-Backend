-- =============================================
-- Consulta de referencia: Clientas de Oro
-- Descripción: Clientes ordenados por gasto total con métricas
--              de frecuencia y días sin comprar.
-- Nota: La clasificación (VIP/Frecuente/Ocasional/Nuevo) y el
--       semáforo se calculan en la capa de aplicación (AnalisisService).
-- =============================================
SELECT
    p.idpersona,
    p.nombre,
    ISNULL(p.telefono, '')                              AS telefono,
    COUNT(DISTINCT v.idventa)                           AS totalCompras,
    ROUND(SUM(v.total), 2)                             AS totalGastado,
    ROUND(AVG(v.total), 2)                             AS ticketPromedio,
    MAX(v.fecha_hora)                                  AS ultimaCompra,
    DATEDIFF(DAY, MAX(v.fecha_hora), GETDATE())        AS diasSinComprar
FROM dbo.persona p WITH (NOLOCK)
INNER JOIN dbo.venta v WITH (NOLOCK)
    ON v.idcliente = p.idpersona
   AND v.estado   != 'Anulado'
   AND v.idcliente NOT IN (13683, 13745, 13744)  -- cliente contado, media docena y docena
GROUP BY p.idpersona, p.nombre, p.telefono
HAVING COUNT(DISTINCT v.idventa) >= 1
ORDER BY totalGastado DESC;
