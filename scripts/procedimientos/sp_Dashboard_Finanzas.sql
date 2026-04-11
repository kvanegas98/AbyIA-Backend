-- =============================================
-- SP: sp_Dashboard_Finanzas
-- Descripción: Datos para gráfico de Ingresos vs Egresos por día
-- Parámetros: @Dias (rango de fechas a mostrar)
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Dashboard_Finanzas') AND type = N'P')
    DROP PROCEDURE dbo.sp_Dashboard_Finanzas;
GO

CREATE PROCEDURE dbo.sp_Dashboard_Finanzas
    @Dias INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Desde DATE = DATEADD(DAY, -@Dias, CAST(GETDATE() AS DATE));
    DECLARE @Hasta DATE = DATEADD(DAY, 1, CAST(GETDATE() AS DATE));

    -- Ingresos (Ventas)
    SELECT 
        CAST(v.fecha_hora AS DATE) AS Fecha,
        SUM(v.total) AS Total
    FROM dbo.venta v WITH (NOLOCK)
    WHERE v.fecha_hora >= @Desde 
      AND v.fecha_hora < @Hasta
      AND v.estado != 'Anulado'
    GROUP BY CAST(v.fecha_hora AS DATE)
    ORDER BY Fecha;

    -- Egresos (Compras/Ingresos de Mercancía)
    SELECT 
        CAST(i.fecha_hora AS DATE) AS Fecha,
        SUM(i.total) AS Total
    FROM dbo.ingreso i WITH (NOLOCK)
    WHERE i.fecha_hora >= @Desde 
      AND i.fecha_hora < @Hasta
      AND i.estado != 'Anulado'
    GROUP BY CAST(i.fecha_hora AS DATE)
    ORDER BY Fecha;
END
GO
