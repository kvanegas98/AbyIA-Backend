-- =============================================
-- SP: sp_Finanzas_EstadoCuentaCliente
-- Descripción: Estado de cuenta con saldo anterior y movimientos con saldo acumulado
-- Basado en: CTE de EstadoCuentaCliente del sistema actual
-- Parámetros: @idCliente, @fechaInicio, @fechaFin
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Finanzas_EstadoCuentaCliente') AND type = N'P')
    DROP PROCEDURE dbo.sp_Finanzas_EstadoCuentaCliente;
GO

CREATE PROCEDURE dbo.sp_Finanzas_EstadoCuentaCliente
    @idCliente   INT,
    @fechaInicio DATETIME,
    @fechaFin    DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    -- Ajustar fechaFin para incluir todo el día
    SET @fechaFin = DATEADD(SECOND, -1, DATEADD(DAY, 1, CAST(@fechaFin AS DATE)));
    SET @fechaInicio = CAST(@fechaInicio AS DATE);

    ;WITH Movimientos1 AS (
        -- Ventas (Aumentan el saldo / Débitos)
        SELECT 
            CAST(v.fecha_hora AS DATETIME) AS fecha_hora,
            'Factura'                       AS tipo_movimiento,
            v.idcliente,
            v.IdCredito,
            v.CodigoFactura                 AS num_documento,
            C.Id_Estado,
            v.total                         AS MontoDebito,
            NULL                            AS MontoCredito
        FROM dbo.venta v
        INNER JOIN dbo.credito C ON C.Id_Credito = v.IdCredito
        WHERE v.idcliente = @idCliente 
            AND v.estado != 'Anulado' 
            AND v.tipo_comprobante = 'CREDITO'

        UNION ALL

        -- Abonos (Disminuyen el saldo / Créditos)
        SELECT 
            CAST(A.FechaDePago AS DATETIME) AS fecha_hora,
            'Abono'                          AS tipo_movimiento,
            C.Id_Persona                     AS idcliente,
            A.Id_Credito,
            A.CodigoAbono                    AS num_documento,
            A.Id_Estado,
            NULL                             AS MontoDebito,
            A.Monto                          AS MontoCredito
        FROM dbo.abono A
        INNER JOIN dbo.credito C ON C.Id_Credito = A.Id_Credito
        WHERE C.Id_Persona = @idCliente 
            AND A.Id_Estado != 2

        UNION ALL

        -- Notas de Crédito (Disminuyen el saldo / Créditos)
        SELECT 
            CAST(nc.FechaCreacion AS DATETIME) AS fecha_hora,
            'Nota de Crédito'                   AS tipo_movimiento,
            v.idcliente,
            v.IdCredito,
            nc.Codigo                            AS num_documento,
            C.Id_Estado,
            NULL                                 AS MontoDebito,
            nc.Total                             AS MontoCredito
        FROM dbo.NotaCredito nc
        INNER JOIN dbo.venta v ON nc.IdVenta = v.idventa
        INNER JOIN dbo.credito C ON v.IdCredito = C.Id_Credito
        WHERE v.idcliente = @idCliente 
            AND v.tipo_comprobante = 'CREDITO'
            AND nc.estado != 'Anulado'
    ),

    -- Saldo anterior al rango de fechas
    SaldoAnterior AS (
        SELECT 
            ISNULL(SUM(COALESCE(MontoDebito, 0) - COALESCE(MontoCredito, 0)), 0) AS SaldoAnterior
        FROM Movimientos1
        WHERE fecha_hora < @fechaInicio
    ),

    -- Movimientos dentro del rango con saldo acumulado
    MovimientosConSaldo AS (
        SELECT 
            M.fecha_hora,
            M.tipo_movimiento,
            M.idcliente,
            M.IdCredito,
            COALESCE(M.num_documento, '-') AS num_documento,
            M.Id_Estado,
            COALESCE(M.MontoDebito, 0)     AS MontoDebito,
            COALESCE(M.MontoCredito, 0)    AS MontoCredito,
            SUM(COALESCE(M.MontoDebito, 0) - COALESCE(M.MontoCredito, 0)) 
                OVER (PARTITION BY M.idcliente ORDER BY M.fecha_hora 
                      ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) 
                + COALESCE(SA.SaldoAnterior, 0) AS Saldo
        FROM Movimientos1 M
        CROSS JOIN SaldoAnterior SA
        WHERE M.fecha_hora BETWEEN @fechaInicio AND @fechaFin
    )

    -- Saldo anterior como primera fila + movimientos
    SELECT 
        NULL           AS fecha_hora, 
        'Saldo Anterior' AS tipo_movimiento, 
        @idCliente     AS idcliente, 
        NULL           AS IdCredito, 
        NULL           AS num_documento, 
        NULL           AS Id_Estado, 
        0              AS MontoDebito, 
        0              AS MontoCredito,
        COALESCE(SaldoAnterior, 0) AS Saldo
    FROM SaldoAnterior

    UNION ALL

    SELECT * FROM MovimientosConSaldo

    ORDER BY fecha_hora;
END
GO
