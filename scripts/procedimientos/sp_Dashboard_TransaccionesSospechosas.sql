-- =============================================
-- SP: sp_Dashboard_TransaccionesSospechosas
-- Descripción: Detecta transacciones que requieren revisión
-- Alertas: Notas de crédito altas, Descuentos excesivos
-- Parámetros: @Dias (rango de fechas)
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Dashboard_TransaccionesSospechosas') AND type = N'P')
    DROP PROCEDURE dbo.sp_Dashboard_TransaccionesSospechosas;
GO

CREATE PROCEDURE dbo.sp_Dashboard_TransaccionesSospechosas
    @Dias INT = 7
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Desde DATE = DATEADD(DAY, -@Dias, CAST(GETDATE() AS DATE));

    -- 1. Notas de Crédito con montos altos (> 1000)
    SELECT 
        'Nota de Crédito' AS Tipo,
        ISNULL(p.nombre, 'Cliente General') AS Cliente,
        'Devolución por valor alto' AS Motivo,
        nc.Total AS Monto,
        CASE 
            WHEN nc.Total > 5000 THEN 'Alto' 
            WHEN nc.Total > 2000 THEN 'Medio'
            ELSE 'Bajo'
        END AS Riesgo,
        nc.FechaCreacion AS Fecha
    FROM dbo.NotaCredito nc WITH (NOLOCK)
    INNER JOIN dbo.venta v WITH (NOLOCK) ON nc.IdVenta = v.idventa
    LEFT JOIN dbo.persona p WITH (NOLOCK) ON v.idcliente = p.idpersona
    WHERE nc.FechaCreacion >= @Desde 
      AND nc.Total > 1000
      AND nc.estado != 'Anulado'

    UNION ALL

    -- 2. Ventas con Descuentos Altos (si existe columna descuento en detalle_venta)
    SELECT 
        'Descuento Alto' AS Tipo,
        ISNULL(p.nombre, 'Cliente General') AS Cliente,
        'Descuento > 20% del subtotal' AS Motivo,
        SUM(dv.descuento) AS Monto,
        CASE 
            WHEN SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.5 THEN 'Alto'
            WHEN SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.3 THEN 'Medio'
            ELSE 'Bajo'
        END AS Riesgo,
        v.fecha_hora AS Fecha
    FROM dbo.venta v WITH (NOLOCK)
    INNER JOIN dbo.detalle_venta dv WITH (NOLOCK) ON v.idventa = dv.idventa
    LEFT JOIN dbo.persona p WITH (NOLOCK) ON v.idcliente = p.idpersona
    WHERE v.fecha_hora >= @Desde
      AND v.estado != 'Anulado'
    GROUP BY v.idventa, p.nombre, v.fecha_hora
    HAVING SUM(dv.descuento) > SUM(dv.cantidad * dv.precio) * 0.2

    UNION ALL

    -- 3. Notas de Crédito Tardías (creadas >10 días después de la venta original)
    SELECT 
        'Nota de Crédito Tardía' AS Tipo,
        ISNULL(p.nombre, 'Cliente General') AS Cliente,
        CONCAT('NC creada ', DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion), ' días después de la venta') AS Motivo,
        nc.Total AS Monto,
        CASE 
            WHEN DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 60 THEN 'Alto'
            WHEN DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 30 THEN 'Medio'
            ELSE 'Bajo'
        END AS Riesgo,
        nc.FechaCreacion AS Fecha
    FROM dbo.NotaCredito nc WITH (NOLOCK)
    INNER JOIN dbo.venta v WITH (NOLOCK) ON nc.IdVenta = v.idventa
    LEFT JOIN dbo.persona p WITH (NOLOCK) ON v.idcliente = p.idpersona
    WHERE nc.FechaCreacion >= @Desde
      AND nc.estado != 'Anulado'
      AND DATEDIFF(DAY, v.fecha_hora, nc.FechaCreacion) > 10

    ORDER BY Fecha DESC;
END
GO
