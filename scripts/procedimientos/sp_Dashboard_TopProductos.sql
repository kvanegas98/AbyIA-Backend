-- =============================================
-- SP: sp_Dashboard_TopProductos
-- Descripción: Top N productos más vendidos (cantidad y monto)
-- Parámetros: @Top (cantidad de productos), @Dias (rango de fechas)
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Dashboard_TopProductos') AND type = N'P')
    DROP PROCEDURE dbo.sp_Dashboard_TopProductos;
GO

CREATE PROCEDURE dbo.sp_Dashboard_TopProductos
    @Top INT = 5,
    @Dias INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Desde DATE = DATEADD(DAY, -@Dias, CAST(GETDATE() AS DATE));

    SELECT TOP (@Top)
        a.nombre AS Producto,
        ISNULL(c.nombre, 'Sin Categoría') AS Categoria,
        SUM(dv.cantidad) AS CantidadVendida,
        SUM(dv.cantidad * dv.precio) AS IngresoTotal
    FROM dbo.detalle_venta dv WITH (NOLOCK)
    INNER JOIN dbo.venta v WITH (NOLOCK) ON dv.idventa = v.idventa
    INNER JOIN dbo.articulo a WITH (NOLOCK) ON dv.idarticulo = a.idarticulo
    LEFT JOIN dbo.categoria c WITH (NOLOCK) ON a.idcategoria = c.idcategoria
    WHERE v.fecha_hora >= @Desde
      AND v.estado != 'Anulado'
    GROUP BY a.nombre, c.nombre
    ORDER BY CantidadVendida DESC;
END
GO
