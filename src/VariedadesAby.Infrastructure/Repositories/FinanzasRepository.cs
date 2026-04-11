using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Finanzas;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Repositories;

public class FinanzasRepository : IFinanzasRepository
{
    private readonly IDbConnection _connection;

    public FinanzasRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    // ── sp_Finanzas_CarteraClientes ──────────────────────────────────────────
    public async Task<IEnumerable<CarteraClienteDto>> GetCarteraClientesAsync()
    {
        const string sql = @"
            SELECT
                cr.Id_Credito                                                       AS IdCredito,
                p.idpersona                                                          AS IdPersona,
                p.nombre                                                             AS NombreCliente,
                p.telefono                                                           AS Telefono,
                COALESCE(ventas.TotalVentas,  0)                                    AS TotalAPagar,
                COALESCE(abonos.TotalAbonos,  0)                                    AS TotalAbonado,
                COALESCE(notas.TotalNotas,    0)                                    AS TotalNotaCredito,
                COALESCE(ventas.TotalVentas,  0)
                    - COALESCE(abonos.TotalAbonos, 0)
                    - COALESCE(notas.TotalNotas,   0)                               AS Pendiente,
                abonos.UltimoAbono                                                  AS FechaUltimoAbono,
                COALESCE(abonos.CantidadAbonos, 0)                                  AS CantidadAbonos,
                COALESCE(ventas.CantidadVentas, 0)                                  AS CantidadVentas,
                CAST(CASE
                    WHEN abonos.UltimoAbono IS NULL THEN 1
                    WHEN DATEDIFF(DAY, abonos.UltimoAbono, GETDATE()) > 7 THEN 1
                    ELSE 0
                END AS BIT)                                                         AS Alerta,
                cr.PrimerCredito
            FROM dbo.credito cr WITH (NOLOCK)
            INNER JOIN dbo.persona p WITH (NOLOCK) ON p.idpersona = cr.Id_Persona
            LEFT JOIN (
                SELECT v.IdCredito, SUM(v.total) AS TotalVentas, COUNT(*) AS CantidadVentas
                FROM dbo.venta v WITH (NOLOCK)
                WHERE v.estado != 'Anulado' AND v.IdCredito IS NOT NULL
                GROUP BY v.IdCredito
            ) ventas ON ventas.IdCredito = cr.Id_Credito
            LEFT JOIN (
                SELECT a.Id_Credito,
                       SUM(a.Monto)        AS TotalAbonos,
                       MAX(a.FechaDePago)  AS UltimoAbono,
                       COUNT(*)            AS CantidadAbonos
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
            WHERE cr.Id_Estado = 1
              AND EXISTS (
                  SELECT 1 FROM dbo.venta v2 WITH (NOLOCK)
                  WHERE v2.IdCredito = cr.Id_Credito AND v2.estado = 'PENDIENTE'
              )
            ORDER BY
                CASE WHEN abonos.UltimoAbono IS NULL THEN 0
                     WHEN DATEDIFF(DAY, abonos.UltimoAbono, GETDATE()) > 7 THEN 1
                     ELSE 2 END,
                (COALESCE(ventas.TotalVentas, 0)
                 - COALESCE(abonos.TotalAbonos, 0)
                 - COALESCE(notas.TotalNotas, 0)) DESC;";

        return await _connection.QueryAsync<CarteraClienteDto>(sql);
    }

    // ── sp_Finanzas_ObtenerAbonos ────────────────────────────────────────────
    public async Task<IEnumerable<AbonoDetalleDto>> GetAbonosAsync()
    {
        const string sql = @"
            SELECT
                a.Id_Abono                                                      AS IdAbono,
                p.nombre                                                        AS NombreCliente,
                v.idcliente                                                     AS IdCliente,
                a.Id_Usuario                                                    AS IdUsuario,
                u.nombre                                                        AS Usuario,
                a.FechaDePago                                                   AS Fecha,
                a.Monto                                                         AS MontoAbono,
                a.CodigoAbono                                                   AS Codigo,
                a.MontoDebido                                                   AS Pendiente,
                a.Id_Estado                                                     AS IdEstado,
                a.TipoPago                                                      AS TipoPago,
                a.DescripcionPago                                               AS DescripcionPago,
                COALESCE((
                    SELECT SUM(ab.Monto) FROM dbo.abono ab WITH (NOLOCK)
                    WHERE ab.Id_Credito = a.Id_Credito AND ab.Id_Estado = 0
                ), 0)                                                           AS MontoAbonado,
                COALESCE((
                    SELECT SUM(nc.Total)
                    FROM dbo.NotaCredito nc WITH (NOLOCK)
                    INNER JOIN dbo.venta vnc WITH (NOLOCK) ON vnc.idventa = nc.IdVenta
                    WHERE vnc.IdCredito = a.Id_Credito AND nc.estado != 'Anulado'
                ), 0)                                                           AS MontoNotaCredito,
                COALESCE((
                    SELECT SUM(v2.total) FROM dbo.venta v2 WITH (NOLOCK)
                    WHERE v2.IdCredito = a.Id_Credito AND v2.estado != 'Anulado'
                ), 0)                                                           AS DeudaInicial,
                (
                    COALESCE((
                        SELECT SUM(v2.total) FROM dbo.venta v2 WITH (NOLOCK)
                        WHERE v2.IdCredito = a.Id_Credito AND v2.estado != 'Anulado'
                    ), 0)
                    - COALESCE((
                        SELECT SUM(ab.Monto) FROM dbo.abono ab WITH (NOLOCK)
                        WHERE ab.Id_Credito = a.Id_Credito AND ab.Id_Estado = 0
                    ), 0)
                    - COALESCE((
                        SELECT SUM(nc.Total)
                        FROM dbo.NotaCredito nc WITH (NOLOCK)
                        INNER JOIN dbo.venta vnc WITH (NOLOCK) ON vnc.idventa = nc.IdVenta
                        WHERE vnc.IdCredito = a.Id_Credito AND nc.estado != 'Anulado'
                    ), 0)
                )                                                               AS DeudaPendiente
            FROM dbo.abono a WITH (NOLOCK)
            INNER JOIN dbo.credito  c WITH (NOLOCK) ON c.Id_Credito  = a.Id_Credito
            INNER JOIN dbo.venta    v WITH (NOLOCK) ON c.Id_Credito  = v.IdCredito
                                                   AND v.estado     != 'Anulado'
            INNER JOIN dbo.persona  p WITH (NOLOCK) ON p.idpersona   = v.idcliente
            INNER JOIN dbo.usuario  u WITH (NOLOCK) ON u.idusuario   = a.Id_Usuario
            WHERE a.Id_Estado = 0
            GROUP BY
                a.Id_Abono, p.nombre, v.idcliente, a.Id_Usuario, a.FechaDePago,
                u.nombre, a.Monto, a.CodigoAbono, a.MontoDebido, a.Id_Estado,
                a.TipoPago, a.DescripcionPago, a.Id_Credito
            ORDER BY a.FechaDePago DESC;";

        return await _connection.QueryAsync<AbonoDetalleDto>(sql);
    }

    // ── sp_Finanzas_EstadoCuentaCliente ──────────────────────────────────────
    public async Task<IEnumerable<EstadoCuentaMovimientoDto>> GetEstadoCuentaClienteAsync(
        int idCliente, DateTime fechaInicio, DateTime fechaFin)
    {
        // Ajustar fechas igual que el SP
        var inicio   = fechaInicio.Date;
        var finDia   = fechaFin.Date.AddDays(1).AddSeconds(-1);

        const string sql = @"
            ;WITH Movimientos1 AS (
                SELECT
                    CAST(v.fecha_hora AS DATETIME)  AS fecha_hora,
                    'Factura'                        AS tipo_movimiento,
                    v.idcliente,
                    v.IdCredito,
                    v.CodigoFactura                 AS num_documento,
                    C.Id_Estado,
                    v.total                         AS MontoDebito,
                    CAST(NULL AS DECIMAL(18,2))     AS MontoCredito
                FROM dbo.venta v WITH (NOLOCK)
                INNER JOIN dbo.credito C WITH (NOLOCK) ON C.Id_Credito = v.IdCredito
                WHERE v.idcliente = @IdCliente
                  AND v.estado != 'Anulado'
                  AND v.tipo_comprobante = 'CREDITO'

                UNION ALL

                SELECT
                    CAST(A.FechaDePago AS DATETIME) AS fecha_hora,
                    'Abono'                          AS tipo_movimiento,
                    C.Id_Persona                     AS idcliente,
                    A.Id_Credito,
                    A.CodigoAbono                   AS num_documento,
                    A.Id_Estado,
                    CAST(NULL AS DECIMAL(18,2))     AS MontoDebito,
                    A.Monto                         AS MontoCredito
                FROM dbo.abono A WITH (NOLOCK)
                INNER JOIN dbo.credito C WITH (NOLOCK) ON C.Id_Credito = A.Id_Credito
                WHERE C.Id_Persona = @IdCliente
                  AND A.Id_Estado != 2

                UNION ALL

                SELECT
                    CAST(nc.FechaCreacion AS DATETIME) AS fecha_hora,
                    'Nota de Crédito'                  AS tipo_movimiento,
                    v.idcliente,
                    v.IdCredito,
                    nc.Codigo                          AS num_documento,
                    C.Id_Estado,
                    CAST(NULL AS DECIMAL(18,2))        AS MontoDebito,
                    nc.Total                           AS MontoCredito
                FROM dbo.NotaCredito nc WITH (NOLOCK)
                INNER JOIN dbo.venta    v WITH (NOLOCK) ON nc.IdVenta    = v.idventa
                INNER JOIN dbo.credito  C WITH (NOLOCK) ON v.IdCredito   = C.Id_Credito
                WHERE v.idcliente = @IdCliente
                  AND v.tipo_comprobante = 'CREDITO'
                  AND nc.estado != 'Anulado'
            ),
            SaldoAnterior AS (
                SELECT ISNULL(SUM(COALESCE(MontoDebito, 0) - COALESCE(MontoCredito, 0)), 0) AS SaldoAnterior
                FROM Movimientos1
                WHERE fecha_hora < @Inicio
            ),
            MovimientosConSaldo AS (
                SELECT
                    M.fecha_hora,
                    M.tipo_movimiento,
                    M.idcliente,
                    M.IdCredito,
                    COALESCE(M.num_documento, '-')  AS num_documento,
                    M.Id_Estado,
                    COALESCE(M.MontoDebito,  0)     AS MontoDebito,
                    COALESCE(M.MontoCredito, 0)     AS MontoCredito,
                    SUM(COALESCE(M.MontoDebito, 0) - COALESCE(M.MontoCredito, 0))
                        OVER (PARTITION BY M.idcliente ORDER BY M.fecha_hora
                              ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)
                        + COALESCE(SA.SaldoAnterior, 0)                        AS Saldo
                FROM Movimientos1 M
                CROSS JOIN SaldoAnterior SA
                WHERE M.fecha_hora BETWEEN @Inicio AND @FinDia
            )
            SELECT
                NULL             AS FechaHora,
                'Saldo Anterior' AS TipoMovimiento,
                @IdCliente       AS IdCliente,
                NULL             AS IdCredito,
                NULL             AS NumDocumento,
                NULL             AS IdEstado,
                CAST(0 AS DECIMAL(18,2)) AS MontoDebito,
                CAST(0 AS DECIMAL(18,2)) AS MontoCredito,
                COALESCE(SaldoAnterior, 0) AS Saldo
            FROM SaldoAnterior

            UNION ALL

            SELECT
                fecha_hora      AS FechaHora,
                tipo_movimiento AS TipoMovimiento,
                idcliente       AS IdCliente,
                IdCredito,
                num_documento   AS NumDocumento,
                Id_Estado       AS IdEstado,
                MontoDebito,
                MontoCredito,
                Saldo
            FROM MovimientosConSaldo

            ORDER BY FechaHora;";

        return await _connection.QueryAsync<EstadoCuentaMovimientoDto>(
            sql,
            new { IdCliente = idCliente, Inicio = inicio, FinDia = finDia });
    }

    // ── sp_Dashboard_Finanzas_Resumen ────────────────────────────────────────
    public async Task<FinanzasResumenDto> GetResumenFinanzasAsync()
    {
        const string sql = @"
            ;WITH SaldosCredito AS (
                SELECT
                    cr.Id_Credito,
                    cr.PrimerCredito,
                    MAX(ab.FechaDePago) AS UltimoAbono,
                    (
                        COALESCE((
                            SELECT SUM(v.total) FROM dbo.venta v
                            WHERE v.IdCredito = cr.Id_Credito AND v.estado != 'Anulado'
                        ), 0)
                        - COALESCE((
                            SELECT SUM(ab2.Monto) FROM dbo.abono ab2
                            WHERE ab2.Id_Credito = cr.Id_Credito AND ab2.Id_Estado = 0
                        ), 0)
                        - COALESCE((
                            SELECT SUM(nc.Total)
                            FROM dbo.NotaCredito nc
                            INNER JOIN dbo.venta v ON nc.IdVenta = v.idventa
                            WHERE v.IdCredito = cr.Id_Credito AND nc.estado != 'Anulado'
                        ), 0)
                    ) AS SaldoPendiente
                FROM dbo.credito cr
                LEFT JOIN dbo.abono ab ON ab.Id_Credito = cr.Id_Credito AND ab.Id_Estado = 0
                WHERE cr.Id_Estado = 1
                GROUP BY cr.Id_Credito, cr.PrimerCredito
            )
            SELECT
                ISNULL(SUM(SaldoPendiente), 0) AS TotalCartera,
                ISNULL(SUM(CASE
                    WHEN SaldoPendiente > 0 AND (
                        (UltimoAbono IS NOT NULL AND DATEDIFF(DAY, UltimoAbono, GETDATE()) > 15)
                        OR (UltimoAbono IS NULL  AND DATEDIFF(DAY, PrimerCredito, GETDATE()) > 15)
                    ) THEN SaldoPendiente * 0.15
                    ELSE 0
                END), 0) AS CobrosProyectados7d,
                ISNULL(COUNT(CASE
                    WHEN SaldoPendiente > 0 AND (
                        (UltimoAbono IS NOT NULL AND DATEDIFF(DAY, UltimoAbono, GETDATE()) > 30)
                        OR (UltimoAbono IS NULL  AND DATEDIFF(DAY, PrimerCredito, GETDATE()) > 30)
                    ) THEN 1
                    ELSE NULL
                END), 0) AS ClientesMorosos
            FROM SaldosCredito
            WHERE SaldoPendiente > 0;";

        var result = await _connection.QueryFirstOrDefaultAsync<FinanzasResumenDto>(sql);
        return result ?? new FinanzasResumenDto();
    }

    // ── sp_Dashboard_Finanzas ────────────────────────────────────────────────
    public async Task<IEnumerable<FinanzasFlujoCajaDto>> GetFlujoCajaAsync(int dias)
    {
        var desde = DateTime.Today.AddDays(-dias);
        var hasta = DateTime.Today.AddDays(1);

        const string sql = @"
            SELECT CAST(v.fecha_hora AS DATE) AS Fecha, SUM(v.total) AS Total
            FROM dbo.venta v WITH (NOLOCK)
            WHERE v.fecha_hora >= @Desde AND v.fecha_hora < @Hasta
              AND v.estado != 'Anulado'
            GROUP BY CAST(v.fecha_hora AS DATE)
            ORDER BY Fecha;

            SELECT CAST(i.fecha_hora AS DATE) AS Fecha, SUM(i.total) AS Total
            FROM dbo.ingreso i WITH (NOLOCK)
            WHERE i.fecha_hora >= @Desde AND i.fecha_hora < @Hasta
              AND i.estado != 'Anulado'
            GROUP BY CAST(i.fecha_hora AS DATE)
            ORDER BY Fecha;";

        using var multi = await _connection.QueryMultipleAsync(sql, new { Desde = desde, Hasta = hasta });
        var rawIngresos = (await multi.ReadAsync<FinanzasFechaTotal>()).ToList();
        var rawEgresos  = (await multi.ReadAsync<FinanzasFechaTotal>()).ToList();

        var fechas = rawIngresos.Select(x => x.Fecha)
            .Union(rawEgresos.Select(x => x.Fecha))
            .Distinct()
            .OrderBy(f => f);

        return fechas.Select(f => new FinanzasFlujoCajaDto
        {
            Fecha    = f,
            Ingresos = rawIngresos.FirstOrDefault(x => x.Fecha == f)?.Total ?? 0,
            Egresos  = rawEgresos.FirstOrDefault(x  => x.Fecha == f)?.Total ?? 0
        }).ToList();
    }
}

file sealed class FinanzasFechaTotal
{
    public DateTime Fecha { get; init; }
    public decimal  Total { get; init; }
}
