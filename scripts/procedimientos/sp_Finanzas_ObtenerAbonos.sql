-- =============================================
-- SP: sp_Finanzas_ObtenerAbonos
-- Descripción: Lista todos los abonos activos con detalle de deuda
-- Basado en: Query ObtenerAbonos del sistema actual
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Finanzas_ObtenerAbonos') AND type = N'P')
    DROP PROCEDURE dbo.sp_Finanzas_ObtenerAbonos;
GO

CREATE PROCEDURE dbo.sp_Finanzas_ObtenerAbonos
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        a.Id_Abono          AS IdAbono,
        p.nombre             AS NombreCliente,
        v.idcliente          AS IdCliente,
        a.Id_Usuario         AS IdUsuario,
        u.nombre             AS Usuario,
        a.FechaDePago        AS Fecha,
        a.Monto              AS MontoAbono,
        a.CodigoAbono        AS Codigo,
        a.MontoDebido        AS Pendiente,
        a.Id_Estado          AS IdEstado,
        a.TipoPago           AS TipoPago,
        a.DescripcionPago    AS DescripcionPago,

        -- Total abonado por crédito
        COALESCE((
            SELECT SUM(ab.Monto) 
            FROM dbo.abono ab 
            WHERE ab.Id_Credito = a.Id_Credito AND ab.Id_Estado = 0
        ), 0) AS MontoAbonado,

        -- Total notas de crédito del crédito
        COALESCE((
            SELECT SUM(nc.Total)
            FROM dbo.NotaCredito nc
            INNER JOIN dbo.venta vnc ON vnc.idventa = nc.IdVenta
            WHERE vnc.IdCredito = a.Id_Credito AND nc.estado != 'Anulado'
        ), 0) AS MontoNotaCredito,

        -- Total de ventas asociadas al crédito (deuda inicial)
        COALESCE((
            SELECT SUM(v2.total)
            FROM dbo.venta v2
            WHERE v2.IdCredito = a.Id_Credito AND v2.estado != 'Anulado'
        ), 0) AS DeudaInicial,

        -- Deuda pendiente = ventas - abonos - notas
        (
            COALESCE((
                SELECT SUM(v2.total)
                FROM dbo.venta v2
                WHERE v2.IdCredito = a.Id_Credito AND v2.estado != 'Anulado'
            ), 0)
            -
            COALESCE((
                SELECT SUM(ab.Monto)
                FROM dbo.abono ab
                WHERE ab.Id_Credito = a.Id_Credito AND ab.Id_Estado = 0
            ), 0)
            -
            COALESCE((
                SELECT SUM(nc.Total)
                FROM dbo.NotaCredito nc
                INNER JOIN dbo.venta vnc ON vnc.idventa = nc.IdVenta
                WHERE vnc.IdCredito = a.Id_Credito AND nc.estado != 'Anulado'
            ), 0)
        ) AS DeudaPendiente

    FROM dbo.abono a
    INNER JOIN dbo.credito c ON c.Id_Credito = a.Id_Credito
    INNER JOIN dbo.venta v ON c.Id_Credito = v.IdCredito AND v.estado != 'Anulado'
    INNER JOIN dbo.persona p ON p.idpersona = v.idcliente
    INNER JOIN dbo.usuario u ON u.idusuario = a.Id_Usuario

    WHERE a.Id_Estado = 0

    GROUP BY 
        a.Id_Abono, p.nombre, v.idcliente, a.Id_Usuario, a.FechaDePago, 
        u.nombre, a.Monto, a.CodigoAbono, a.MontoDebido, a.Id_Estado, 
        a.TipoPago, a.DescripcionPago, a.Id_Credito

    ORDER BY a.FechaDePago DESC;
END
GO
