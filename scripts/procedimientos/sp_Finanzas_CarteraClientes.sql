-- =============================================
-- SP: sp_Finanzas_CarteraClientes
-- Descripción: Lista de clientes con crédito activo y saldo pendiente
-- Fórmula: Pendiente = SUM(Ventas crédito) - SUM(Abonos Id_Estado=0) - SUM(NotasCredito)
-- Alerta: Si último abono fue hace más de 7 días
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Finanzas_CarteraClientes') AND type = N'P')
    DROP PROCEDURE dbo.sp_Finanzas_CarteraClientes;
GO

CREATE PROCEDURE dbo.sp_Finanzas_CarteraClientes
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        cr.Id_Credito       AS IdCredito,
        p.idpersona          AS IdPersona,
        p.nombre             AS NombreCliente,
        p.telefono           AS Telefono,

        -- Total de ventas a crédito (no anuladas)
        COALESCE(ventas.TotalVentas, 0) AS TotalAPagar,

        -- Total abonos activos
        COALESCE(abonos.TotalAbonos, 0) AS TotalAbonado,

        -- Total notas de crédito
        COALESCE(notas.TotalNotas, 0) AS TotalNotaCredito,

        -- Saldo pendiente
        COALESCE(ventas.TotalVentas, 0) 
            - COALESCE(abonos.TotalAbonos, 0) 
            - COALESCE(notas.TotalNotas, 0) AS Pendiente,

        -- Último abono
        abonos.UltimoAbono AS FechaUltimoAbono,

        -- Cantidad de abonos realizados
        COALESCE(abonos.CantidadAbonos, 0) AS CantidadAbonos,

        -- Cantidad de ventas a crédito
        COALESCE(ventas.CantidadVentas, 0) AS CantidadVentas,

        -- Alerta: sin abono hace más de 7 días
        CASE 
            WHEN abonos.UltimoAbono IS NULL THEN 1
            WHEN DATEDIFF(DAY, abonos.UltimoAbono, GETDATE()) > 7 THEN 1
            ELSE 0
        END AS Alerta,

        -- Primer crédito
        cr.PrimerCredito

    FROM dbo.credito cr
    INNER JOIN dbo.persona p ON p.idpersona = cr.Id_Persona

    -- Ventas a crédito no anuladas
    LEFT JOIN (
        SELECT 
            v.IdCredito,
            SUM(v.total) AS TotalVentas,
            COUNT(*) AS CantidadVentas
        FROM dbo.venta v
        WHERE v.estado != 'Anulado' 
          AND v.IdCredito IS NOT NULL
        GROUP BY v.IdCredito
    ) ventas ON ventas.IdCredito = cr.Id_Credito

    -- Abonos activos (Id_Estado = 0)
    LEFT JOIN (
        SELECT 
            a.Id_Credito,
            SUM(a.Monto) AS TotalAbonos,
            MAX(a.FechaDePago) AS UltimoAbono,
            COUNT(*) AS CantidadAbonos
        FROM dbo.abono a
        WHERE a.Id_Estado = 0
        GROUP BY a.Id_Credito
    ) abonos ON abonos.Id_Credito = cr.Id_Credito

    -- Notas de crédito no anuladas
    LEFT JOIN (
        SELECT 
            v.IdCredito,
            SUM(nc.Total) AS TotalNotas
        FROM dbo.NotaCredito nc
        INNER JOIN dbo.venta v ON v.idventa = nc.IdVenta
        WHERE nc.estado != 'Anulado' 
          AND v.IdCredito IS NOT NULL
        GROUP BY v.IdCredito
    ) notas ON notas.IdCredito = cr.Id_Credito

    WHERE cr.Id_Estado = 1
      -- Solo créditos con ventas pendientes
      AND EXISTS (
          SELECT 1 FROM dbo.venta v2 
          WHERE v2.IdCredito = cr.Id_Credito 
            AND v2.estado = 'PENDIENTE'
      )

    ORDER BY 
        CASE WHEN abonos.UltimoAbono IS NULL THEN 0
             WHEN DATEDIFF(DAY, abonos.UltimoAbono, GETDATE()) > 7 THEN 1
             ELSE 2
        END,
        (COALESCE(ventas.TotalVentas, 0) - COALESCE(abonos.TotalAbonos, 0) - COALESCE(notas.TotalNotas, 0)) DESC;
END
GO
