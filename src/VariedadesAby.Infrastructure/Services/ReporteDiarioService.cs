using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VariedadesAby.Core.DTOs.FtpDrive;
using VariedadesAby.Core.DTOs.Reporte;
using VariedadesAby.Core.Interfaces;
using VariedadesAby.Infrastructure.Helpers;

namespace VariedadesAby.Infrastructure.Services;

public sealed class ReporteDiarioService : IReporteDiarioService
{
    private readonly IDbConnection _db;
    private readonly IEmailNotificationService _email;
    private readonly SchedulerSettings _schedulerSettings;
    private readonly ILogger<ReporteDiarioService> _logger;

    public ReporteDiarioService(
        IDbConnection db,
        IEmailNotificationService email,
        IOptions<SchedulerSettings> schedulerSettings,
        ILogger<ReporteDiarioService> logger)
    {
        _db = db;
        _email = email;
        _schedulerSettings = schedulerSettings.Value;
        _logger = logger;
    }

    public async Task<ReporteEnvioResultado> GenerarYEnviarAsync(CancellationToken ct = default)
    {
        // Fecha "hoy" en hora Nicaragua
        var hoy = TimeZoneHelper.NowIn(_schedulerSettings.TimeZoneId).Date;

        _logger.LogInformation("[Reporte] Generando reporte diario para {Fecha:yyyy-MM-dd}", hoy);

        const string sql = @"
            -- 1. Resumen global de ventas del día
            SELECT
                COUNT(*)                                                                             AS TotalVentas,
                ISNULL(SUM(total), 0)                                                                AS TotalGlobal,
                ISNULL(SUM(CASE WHEN tipo_comprobante != 'CREDITO' THEN total ELSE 0 END), 0)        AS TotalContado,
                ISNULL(SUM(CASE WHEN tipo_comprobante  = 'CREDITO' THEN total ELSE 0 END), 0)        AS TotalCredito
            FROM dbo.venta WITH (NOLOCK)
            WHERE CAST(fecha_hora AS DATE) = @Hoy AND estado != 'Anulado';

            -- 2. Ventas por sucursal del día
            SELECT
                s.nombre                                                                             AS Sucursal,
                COUNT(CASE WHEN v.tipo_comprobante != 'CREDITO' THEN 1 END)                          AS VentasContado,
                ISNULL(SUM(CASE WHEN v.tipo_comprobante != 'CREDITO' THEN v.total END), 0)           AS TotalContado,
                COUNT(CASE WHEN v.tipo_comprobante  = 'CREDITO' THEN 1 END)                          AS VentasCredito,
                ISNULL(SUM(CASE WHEN v.tipo_comprobante  = 'CREDITO' THEN v.total END), 0)           AS TotalCredito,
                COUNT(v.idventa)                                                                     AS TotalVentas,
                ISNULL(SUM(v.total), 0)                                                              AS TotalGlobal
            FROM dbo.sucursal s WITH (NOLOCK)
            LEFT JOIN dbo.usuario u WITH (NOLOCK) ON u.idsucursal = s.idsucursal
            LEFT JOIN dbo.venta   v WITH (NOLOCK) ON v.idusuario  = u.idusuario
                AND CAST(v.fecha_hora AS DATE) = @Hoy
                AND v.estado != 'Anulado'
            GROUP BY s.idsucursal, s.nombre
            ORDER BY TotalGlobal DESC;

            -- 3. Devoluciones (NotaCredito) del día
            SELECT
                COUNT(*)                  AS Cantidad,
                ISNULL(SUM(Total), 0)     AS MontoTotal
            FROM dbo.NotaCredito WITH (NOLOCK)
            WHERE CAST(FechaCreacion AS DATE) = @Hoy AND estado != 'Anulado';

            -- 4. Abonos registrados hoy
            SELECT
                COUNT(*)                  AS Cantidad,
                ISNULL(SUM(Monto), 0)     AS MontoTotal
            FROM dbo.abono WITH (NOLOCK)
            WHERE CAST(FechaDePago AS DATE) = @Hoy AND Id_Estado = 0;

            -- 5. Cartera activa total (saldo pendiente > 0)
            SELECT
                COUNT(*)                                                                                                                AS ClientesConSaldo,
                ISNULL(SUM(ISNULL(v_tot.TotalAPagar, 0) - ISNULL(ab_tot.TotalAbonos, 0) - ISNULL(nc_tot.TotalNC, 0)), 0)               AS SaldoTotal
            FROM dbo.credito c WITH (NOLOCK)
            LEFT JOIN (
                SELECT IdCredito, SUM(total) AS TotalAPagar
                FROM dbo.venta WITH (NOLOCK)
                WHERE estado != 'Anulado'
                GROUP BY IdCredito
            ) v_tot  ON v_tot.IdCredito  = c.Id_Credito
            LEFT JOIN (
                SELECT Id_Credito, SUM(Monto) AS TotalAbonos
                FROM dbo.abono WITH (NOLOCK)
                WHERE Id_Estado = 0
                GROUP BY Id_Credito
            ) ab_tot ON ab_tot.Id_Credito = c.Id_Credito
            LEFT JOIN (
                SELECT v.IdCredito, SUM(nc.Total) AS TotalNC
                FROM dbo.NotaCredito nc WITH (NOLOCK)
                INNER JOIN dbo.venta v WITH (NOLOCK) ON v.idventa = nc.IdVenta
                WHERE nc.estado != 'Anulado'
                GROUP BY v.IdCredito
            ) nc_tot ON nc_tot.IdCredito  = c.Id_Credito
            WHERE c.Id_Estado = 1
              AND (ISNULL(v_tot.TotalAPagar, 0) - ISNULL(ab_tot.TotalAbonos, 0) - ISNULL(nc_tot.TotalNC, 0)) > 0;

            -- 6. Top 10 productos más vendidos hoy
            SELECT TOP 10
                a.nombre                              AS Producto,
                SUM(dv.cantidad)                      AS CantidadVendida,
                ISNULL(SUM(dv.cantidad * dv.precio), 0) AS TotalVendido
            FROM dbo.detalle_venta dv WITH (NOLOCK)
            INNER JOIN dbo.articulo a WITH (NOLOCK) ON a.idarticulo = dv.idarticulo
            INNER JOIN dbo.venta    v WITH (NOLOCK) ON v.idventa    = dv.idventa
            WHERE CAST(v.fecha_hora AS DATE) = @Hoy AND v.estado != 'Anulado'
            GROUP BY a.idarticulo, a.nombre
            ORDER BY CantidadVendida DESC;";

        using var multi = await _db.QueryMultipleAsync(sql, new { Hoy = hoy });

        var ventas      = await multi.ReadFirstAsync<ResumenVentasDto>();
        var sucursales  = (await multi.ReadAsync<VentaSucursalDto>()).ToList();
        var devoluciones = await multi.ReadFirstAsync<ResumenSimpleDto>();
        var abonos      = await multi.ReadFirstAsync<ResumenSimpleDto>();
        var cartera     = await multi.ReadFirstAsync<ResumenCarteraDto>();
        var topProductos = (await multi.ReadAsync<TopProductoReporteDto>()).ToList();

        // No enviar si no hubo ventas
        if (ventas.TotalVentas == 0)
        {
            _logger.LogInformation("[Reporte] Sin ventas para {Fecha:yyyy-MM-dd}. Correo no enviado.", hoy);
            return new ReporteEnvioResultado(false, "Sin ventas registradas hoy. Correo no enviado.", 0, 0);
        }

        var reporte = new ReporteDiarioDto
        {
            Fecha            = hoy,
            Ventas           = ventas,
            VentasPorSucursal = sucursales,
            Devoluciones     = devoluciones,
            Abonos           = abonos,
            Cartera          = cartera,
            TopProductos     = topProductos
        };

        var error = await _email.SendReporteDiarioAsync(reporte, ct);

        if (error is not null)
        {
            _logger.LogError("[Reporte] Fallo al enviar correo: {Error}", error);
            return new ReporteEnvioResultado(false, $"Reporte generado pero el correo falló: {error}", ventas.TotalVentas, ventas.TotalGlobal);
        }

        _logger.LogInformation("[Reporte] Correo enviado. Ventas={Total}, Monto=C${Monto:N2}", ventas.TotalVentas, ventas.TotalGlobal);
        return new ReporteEnvioResultado(true, "Reporte enviado correctamente.", ventas.TotalVentas, ventas.TotalGlobal);
    }
}
