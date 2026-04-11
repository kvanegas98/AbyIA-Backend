-- =============================================
-- SP: sp_Dashboard_Resumen
-- Descripción: KPIs principales para el Dashboard
-- Retorna: VentaHoy, VentaMes, CuentasPorCobrar, StockGlobal
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Dashboard_Resumen') AND type = N'P')
    DROP PROCEDURE dbo.sp_Dashboard_Resumen;
GO

CREATE PROCEDURE dbo.sp_Dashboard_Resumen
    @IdSucursal INT = NULL  -- NULL = todas las sucursales
AS
BEGIN

    DECLARE @Hoy DATE = CAST(GETDATE() AS DATE);
    DECLARE @Ayer DATE = DATEADD(DAY, -1, @Hoy);
    DECLARE @InicioMes DATE = DATEFROMPARTS(YEAR(@Hoy), MONTH(@Hoy), 1);

    -- Venta de Hoy (Total y Cantidad para Ticket Promedio)
    DECLARE @VentaHoy DECIMAL(18,2) = 0;
    DECLARE @CantidadVentasHoy INT = 0;
    SELECT 
        @VentaHoy = ISNULL(SUM(v.total), 0),
        @CantidadVentasHoy = COUNT(*)
    FROM dbo.venta v WITH (NOLOCK)
    WHERE v.fecha_hora >= @Hoy AND v.fecha_hora < DATEADD(DAY, 1, @Hoy)
      AND v.estado != 'Anulado'
      AND (@IdSucursal IS NULL OR v.IdSucursal = @IdSucursal);

    -- Venta de Ayer (Total y Cantidad para comparar)
    DECLARE @VentaAyer DECIMAL(18,2) = 0;
    DECLARE @CantidadVentasAyer INT = 0;
    SELECT 
        @VentaAyer = ISNULL(SUM(v.total), 0),
        @CantidadVentasAyer = COUNT(*)
    FROM dbo.venta v WITH (NOLOCK)
    WHERE v.fecha_hora >= @Ayer AND v.fecha_hora < @Hoy
      AND v.estado != 'Anulado'
      AND (@IdSucursal IS NULL OR v.IdSucursal = @IdSucursal);

    -- Venta del Mes
    DECLARE @VentaMes DECIMAL(18,2) = 0;
    SELECT @VentaMes = ISNULL(SUM(v.total), 0)
    FROM dbo.venta v WITH (NOLOCK)
    WHERE v.fecha_hora >= @InicioMes
      AND v.estado != 'Anulado'
      AND (@IdSucursal IS NULL OR v.IdSucursal = @IdSucursal);

    -- Utilidad del Mes
    DECLARE @UtilidadMes DECIMAL(18,2) = 0;
    SELECT @UtilidadMes = ISNULL(SUM(v.utilidad), 0)
    FROM dbo.venta v WITH (NOLOCK)
    WHERE v.fecha_hora >= @InicioMes
      AND v.estado != 'Anulado'
      AND (@IdSucursal IS NULL OR v.IdSucursal = @IdSucursal);

    -- Cuentas por Cobrar
    -- Fórmula: SUM(Ventas crédito no anuladas) - SUM(Abonos activos) - SUM(NotasCredito no anuladas)
    DECLARE @CuentasPorCobrar DECIMAL(18,2) = 0;
    SELECT @CuentasPorCobrar = ISNULL(SUM(
        COALESCE(ventas.TotalVentas, 0) 
        - COALESCE(abonos.TotalAbonos, 0) 
        - COALESCE(notas.TotalNotas, 0)
    ), 0)
    FROM dbo.credito cr WITH (NOLOCK)
    INNER JOIN (
        SELECT v.IdCredito, SUM(v.total) AS TotalVentas
        FROM dbo.venta v WITH (NOLOCK)
        WHERE v.estado != 'Anulado' AND v.IdCredito IS NOT NULL
        GROUP BY v.IdCredito
    ) ventas ON ventas.IdCredito = cr.Id_Credito
    LEFT JOIN (
        SELECT a.Id_Credito, SUM(a.Monto) AS TotalAbonos
        FROM dbo.abono a WITH (NOLOCK)
        WHERE a.Id_Estado = 0
        GROUP BY a.Id_Credito
    ) abonos ON abonos.Id_Credito = cr.Id_Credito
    LEFT JOIN (
        SELECT v.IdCredito, SUM(nc.Total) AS TotalNotas
        FROM dbo.NotaCredito nc WITH (NOLOCK)
        INNER JOIN dbo.venta v WITH (NOLOCK) ON v.idventa = nc.IdVenta
        WHERE nc.estado != 'Anulado' AND v.IdCredito IS NOT NULL
        GROUP BY v.IdCredito
    ) notas ON notas.IdCredito = cr.Id_Credito
    WHERE cr.Id_Estado = 1;

    -- Stock Global
    DECLARE @StockGlobal INT = 0;
    SELECT @StockGlobal = ISNULL(SUM(sa.stock), 0)
    FROM dbo.sucursalArticulo sa WITH (NOLOCK)
    WHERE (@IdSucursal IS NULL OR sa.idsucursal = @IdSucursal);

    -- Artículos Activos
    DECLARE @ArticulosActivos INT = 0;
    SELECT @ArticulosActivos = COUNT(*)
    FROM dbo.articulo WITH (NOLOCK)
    WHERE condicion = 1;

    -- Stock Crítico (productos con stock <= 5)
    DECLARE @StockCritico INT = 0;
    SELECT @StockCritico = COUNT(DISTINCT sa.idarticulo)
    FROM dbo.sucursalArticulo sa WITH (NOLOCK)
    INNER JOIN dbo.articulo a WITH (NOLOCK) ON sa.idarticulo = a.idarticulo
    WHERE sa.stock <= 5 
      AND a.condicion = 1
      AND (@IdSucursal IS NULL OR sa.idsucursal = @IdSucursal);

    -- Ticket Promedio (Hoy y Ayer)
    DECLARE @TicketPromedioHoy DECIMAL(18,2) = 0;
    DECLARE @TicketPromedioAyer DECIMAL(18,2) = 0;
    
    IF @CantidadVentasHoy > 0
        SET @TicketPromedioHoy = @VentaHoy / @CantidadVentasHoy;
    
    IF @CantidadVentasAyer > 0
        SET @TicketPromedioAyer = @VentaAyer / @CantidadVentasAyer;

    SELECT 
        @VentaHoy           AS VentaHoy,
        @VentaAyer          AS VentaAyer,
        @VentaMes           AS VentaMes,
        @UtilidadMes        AS UtilidadMes,
        @CuentasPorCobrar   AS CuentasPorCobrar,
        @StockGlobal        AS StockGlobal,
        @StockCritico       AS StockCritico,
        @ArticulosActivos   AS ArticulosActivos,
        @TicketPromedioHoy  AS TicketPromedioHoy,
        @TicketPromedioAyer AS TicketPromedioAyer,
        @CantidadVentasHoy  AS CantidadVentasHoy,
        @CantidadVentasAyer AS CantidadVentasAyer;
END
GO
