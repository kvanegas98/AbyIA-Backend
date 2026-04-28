using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Dashboard;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Repositories;

public class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnection _connection;

    public DashboardRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<DashboardResumenDto> GetResumenAsync(int? idSucursal = null)
    {
        var hoy       = DateTime.Today;
        var ayer      = hoy.AddDays(-1);
        var manana    = hoy.AddDays(1);
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

        const string sql = @"
            SELECT
                ISNULL((SELECT SUM(total) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Hoy AND fecha_hora < @Manana
                           AND estado != 'Anulado'
                           AND (@IdSucursal IS NULL OR IdSucursal = @IdSucursal)), 0) AS VentaHoy,
                ISNULL((SELECT COUNT(*)  FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Hoy AND fecha_hora < @Manana
                           AND estado != 'Anulado'
                           AND (@IdSucursal IS NULL OR IdSucursal = @IdSucursal)), 0) AS CantidadVentasHoy,
                ISNULL((SELECT SUM(total) FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Ayer AND fecha_hora < @Hoy
                           AND estado != 'Anulado'
                           AND (@IdSucursal IS NULL OR IdSucursal = @IdSucursal)), 0) AS VentaAyer,
                ISNULL((SELECT COUNT(*)  FROM dbo.venta WITH (NOLOCK)
                         WHERE fecha_hora >= @Ayer AND fecha_hora < @Hoy
                           AND estado != 'Anulado'
                           AND (@IdSucursal IS NULL OR IdSucursal = @IdSucursal)), 0) AS CantidadVentasAyer,
                ISNULL((
                    SELECT COUNT(DISTINCT sa.idarticulo)
                    FROM dbo.sucursalArticulo sa WITH (NOLOCK)
                    INNER JOIN dbo.articulo a WITH (NOLOCK) ON sa.idarticulo = a.idarticulo
                    WHERE sa.stock <= 5 AND a.condicion = 1
                      AND (@IdSucursal IS NULL OR sa.idsucursal = @IdSucursal)
                ), 0) AS StockCritico,
                ISNULL((
                    SELECT SUM(
                        COALESCE(ventas.TotalVentas, 0)
                        - COALESCE(abonos.TotalAbonos, 0)
                        - COALESCE(notas.TotalNotas,   0)
                    )
                    FROM dbo.credito cr WITH (NOLOCK)
                    INNER JOIN (
                        SELECT v.IdCredito, SUM(v.total) AS TotalVentas
                        FROM dbo.venta v WITH (NOLOCK)
                        WHERE v.estado != 'Anulado' AND v.IdCredito IS NOT NULL
                        GROUP BY v.IdCredito
                    ) ventas ON ventas.IdCredito = cr.Id_Credito
                    LEFT JOIN (
                        SELECT ab.Id_Credito, SUM(ab.Monto) AS TotalAbonos
                        FROM dbo.abono ab WITH (NOLOCK)
                        WHERE ab.Id_Estado = 0
                        GROUP BY ab.Id_Credito
                    ) abonos ON abonos.Id_Credito = cr.Id_Credito
                    LEFT JOIN (
                        SELECT v.IdCredito, SUM(nc.Total) AS TotalNotas
                        FROM dbo.NotaCredito nc WITH (NOLOCK)
                        INNER JOIN dbo.venta v WITH (NOLOCK) ON v.idventa = nc.IdVenta
                        WHERE nc.estado != 'Anulado' AND v.IdCredito IS NOT NULL
                        GROUP BY v.IdCredito
                    ) notas ON notas.IdCredito = cr.Id_Credito
                    WHERE cr.Id_Estado = 1
                ), 0) AS CuentasPorCobrar";

        var p = new DynamicParameters();
        p.Add("Hoy",        hoy);
        p.Add("Ayer",       ayer);
        p.Add("Manana",     manana);
        p.Add("InicioMes",  inicioMes);
        p.Add("IdSucursal", idSucursal);

        var raw = await _connection.QueryFirstOrDefaultAsync<DashboardRepositoryRaw>(sql, p);
        if (raw == null) return new DashboardResumenDto();

        decimal ticketHoy  = raw.CantidadVentasHoy  > 0 ? raw.VentaHoy  / raw.CantidadVentasHoy  : 0;
        decimal ticketAyer = raw.CantidadVentasAyer > 0 ? raw.VentaAyer / raw.CantidadVentasAyer : 0;

        return new DashboardResumenDto
        {
            VentaPeriodo                        = raw.VentaHoy,
            CantidadVentasPeriodo               = raw.CantidadVentasHoy,
            TicketPeriodo                       = ticketHoy,
            CuentasPorCobrar                    = raw.CuentasPorCobrar,
            StockCritico                        = raw.StockCritico,
            PorcentajeVentavsPeriodoAnterior    = raw.VentaAyer == 0 ? (raw.VentaHoy > 0 ? 100 : 0)
                                                  : Math.Round(((raw.VentaHoy - raw.VentaAyer) / raw.VentaAyer) * 100, 2),
            PorcentajeTicketvsPeriodoAnterior   = ticketAyer == 0 ? (ticketHoy > 0 ? 100 : 0)
                                                  : Math.Round(((ticketHoy - ticketAyer) / ticketAyer) * 100, 2),
            FechaDesde  = DateTime.Today,
            FechaHasta  = DateTime.Today,
            DiasPeriodo = 1,
        };
    }
}

file sealed class DashboardRepositoryRaw
{
    public decimal VentaHoy           { get; init; }
    public int     CantidadVentasHoy  { get; init; }
    public decimal VentaAyer          { get; init; }
    public int     CantidadVentasAyer { get; init; }
    public int     StockCritico       { get; init; }
    public decimal CuentasPorCobrar   { get; init; }
}
