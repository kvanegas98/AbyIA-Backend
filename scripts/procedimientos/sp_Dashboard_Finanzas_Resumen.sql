-- =============================================
-- SP: sp_Dashboard_Finanzas_Resumen
-- Descripción: Devuelve los KPIs principales del dashboard de finanzas
-- Retorna: 
--  - TotalCartera: Suma total de saldos pendientes de créditos activos
--  - CobrosProyectados7d: Suma de saldos vencidos hace poco + cuotas por vencer en 7 días (Estimación simplificada: 20% del saldo total no vencido + saldo vencido < 30 dias)
--  - ClientesMorosos: Cantidad de clientes con saldo pendiente y último abono > 30 días o crédito muy antiguo sin abonos
-- =============================================
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.sp_Dashboard_Finanzas_Resumen') AND type = N'P')
    DROP PROCEDURE dbo.sp_Dashboard_Finanzas_Resumen;
GO

CREATE PROCEDURE dbo.sp_Dashboard_Finanzas_Resumen
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalCartera DECIMAL(18,2) = 0;
    DECLARE @CobrosProyectados7d DECIMAL(18,2) = 0;
    DECLARE @ClientesMorosos INT = 0;

    -- 1. Calcular Saldo Pendiente por Crédito
    -- Usamos una tabla temporal o CTE para tener los saldos calculados
    ;WITH SaldosCredito AS (
        SELECT 
            cr.Id_Credito,
            cr.Id_Persona,
            MAX(a.FechaDePago) as UltimoAbono,
            cr.PrimerCredito,
            
            -- Lógica exacta del C#: TotalAPagar - Abono - TotalNotaCredito
            (
                COALESCE((SELECT SUM(v.total) FROM dbo.venta v WHERE v.IdCredito = cr.Id_Credito AND v.estado != 'Anulado'), 0)
                - 
                COALESCE((SELECT SUM(a.Monto) FROM dbo.abono a WHERE a.Id_Credito = cr.Id_Credito AND a.Id_Estado = 0), 0)
                - 
                COALESCE((
                    SELECT SUM(nc.Total) 
                    FROM dbo.NotaCredito nc 
                    INNER JOIN dbo.venta v ON nc.IdVenta = v.idventa 
                    WHERE v.IdCredito = cr.Id_Credito AND nc.estado != 'Anulado'
                ), 0)
            ) AS SaldoPendiente

        FROM dbo.credito cr
        LEFT JOIN dbo.abono a ON a.Id_Credito = cr.Id_Credito AND a.Id_Estado = 0
        
        WHERE cr.Id_Estado = 1 -- Créditos Activos
        GROUP BY cr.Id_Credito, cr.Id_Persona, cr.PrimerCredito
    )
    SELECT 
        @TotalCartera = SUM(SaldoPendiente),
        
        -- Clientes Morosos: (Sin abonos > 30 días)
        @ClientesMorosos = COUNT(CASE 
            WHEN SaldoPendiente > 0 AND (
                (UltimoAbono IS NOT NULL AND DATEDIFF(DAY, UltimoAbono, GETDATE()) > 30) OR
                (UltimoAbono IS NULL AND DATEDIFF(DAY, PrimerCredito, GETDATE()) > 30)
            ) THEN 1 
            ELSE NULL 
        END),

        -- Cobros Proyectados (7 días): 
        -- Regla de Negocio: Clientes que no han abonado en más de 15 días.
        -- Proyección: Esperamos recuperar un 15% de su deuda en la próxima semana.
        @CobrosProyectados7d = SUM(CASE
            WHEN SaldoPendiente > 0 AND (
                (UltimoAbono IS NOT NULL AND DATEDIFF(DAY, UltimoAbono, GETDATE()) > 15) OR
                (UltimoAbono IS NULL AND DATEDIFF(DAY, PrimerCredito, GETDATE()) > 15)
            ) THEN SaldoPendiente * 0.15 -- Proyección del 15% del saldo vencido > 15 días
            ELSE 0
        END)

    FROM SaldosCredito
    WHERE SaldoPendiente > 0;

    -- Retornar resultados
    SELECT 
        ISNULL(@TotalCartera, 0) AS TotalCartera,
        ISNULL(@CobrosProyectados7d, 0) AS CobrosProyectados7d,
        ISNULL(@ClientesMorosos, 0) AS ClientesMorosos;
END
GO
