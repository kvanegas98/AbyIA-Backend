using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Vendedores;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class VendedoresService : IVendedoresService
{
    private readonly IDbConnection _db;

    public VendedoresService(IDbConnection db)
    {
        _db = db;
    }

    public async Task<PanelVendedoresDto> GetPanelAsync(FiltroVendedoresViewModel filtro)
    {
        // FechaFin incluye todo el día hasta las 23:59:59
        var fechaFinDia = filtro.FechaFin.Date.AddDays(1);

        var p = new DynamicParameters();
        p.Add("FechaInicio", filtro.FechaInicio.Date);
        p.Add("FechaFin",    fechaFinDia);

        // ── Una sola query: KPIs globales + detalle por vendedor ──────────────
        // Contado = ventas SIN registro en abono (pagadas al instante).
        // Crédito = ventas CON al menos un registro en abono (pago diferido).
        // Filtros: usuario activo, estado de venta válido, rango de fechas.
        // Solo usuarios con ventas > 0 en el rango (HAVING).
        const string sql = @"
            SELECT
                u.idusuario                                                                AS IdUsuario,
                u.nombre                                                                   AS Nombre,
                -- Contado: ventas sin cuenta de crédito asociada (IdCredito IS NULL)
                ISNULL(SUM(CASE WHEN v.IdCredito IS NULL THEN v.total - v.impuesto  ELSE 0 END), 0)    AS TotalContado,
                ISNULL(SUM(CASE WHEN v.IdCredito IS NULL THEN 1                     ELSE 0 END), 0)    AS VentasContado,
                -- Crédito: ventas vinculadas a una cuenta de crédito (IdCredito IS NOT NULL)
                ISNULL(SUM(CASE WHEN v.IdCredito IS NOT NULL THEN v.total - v.impuesto  ELSE 0 END), 0) AS TotalCredito,
                ISNULL(SUM(CASE WHEN v.IdCredito IS NOT NULL THEN 1                     ELSE 0 END), 0) AS VentasCredito,
                -- Totales
                ISNULL(SUM(v.total - v.impuesto), 0) AS Total,
                COUNT(v.idventa)           AS TotalVentas,
                ISNULL(SUM(v.utilidad), 0) AS Utilidad
            FROM dbo.usuario u WITH (NOLOCK)
            INNER JOIN dbo.venta v WITH (NOLOCK)
                ON  v.idusuario   = u.idusuario
                AND v.estado     != 'Anulado'
                AND v.fecha_hora >= @FechaInicio
                AND v.fecha_hora <  @FechaFin
            WHERE u.condicion = 1
            GROUP BY u.idusuario, u.nombre
            HAVING SUM(v.total - v.impuesto) > 0
            ORDER BY Total DESC;";

        var vendedores = (await _db.QueryAsync<VendedorResumenDto>(sql, p)).ToList();

        // ── Calcular KPIs globales a partir del resultado ─────────────────────
        var kpis = new KpisVendedoresDto
        {
            TotalGeneral       = vendedores.Sum(v => v.Total),
            TotalContado       = vendedores.Sum(v => v.TotalContado),
            TotalCredito       = vendedores.Sum(v => v.TotalCredito),
            TotalUtilidad      = vendedores.Sum(v => v.Utilidad),
            TotalVentas        = vendedores.Sum(v => v.TotalVentas),
            TotalVentasContado = vendedores.Sum(v => v.VentasContado),
            TotalVentasCredito = vendedores.Sum(v => v.VentasCredito)
        };

        return new PanelVendedoresDto
        {
            Kpis      = kpis,
            Vendedores = vendedores
        };
    }
}
