using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Ventas;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class VentasService : IVentasService
{
    private readonly IDbConnection _db;

    public VentasService(IDbConnection db)
    {
        _db = db;
    }

    // ──────────────────────────────────────────────────────────────
    // ANULAR VENTA
    // ──────────────────────────────────────────────────────────────
    public async Task AnularAsync(int idVenta, int idSucursal, AnularVentaRequestDto dto)
    {
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var tx = _db.BeginTransaction();
        try
        {
            var venta = await _db.QueryFirstOrDefaultAsync<VentaAnulacion>(
                "SELECT idventa, estado, IdCredito, CodigoFactura FROM dbo.venta WITH (NOLOCK) WHERE idventa = @id",
                new { id = idVenta }, tx);

            if (venta is null)
                throw new KeyNotFoundException($"La venta {idVenta} no existe.");

            if (venta.Estado.Equals("Anulado", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"La venta {idVenta} ya está anulada.");

            var tieneAbonos = await _db.ExecuteScalarAsync<bool>(
                @"SELECT CAST(COUNT(1) AS BIT)
                  FROM dbo.Abono WITH (NOLOCK)
                  WHERE Id_Venta = @idVenta
                    AND Id_Credito = @idCredito
                    AND Id_Estado <> 2",
                new { idVenta, idCredito = venta.IdCredito }, tx);

            if (tieneAbonos)
                throw new InvalidOperationException(
                    $"La factura {venta.CodigoFactura} posee abonos realizados, no se puede anular.");

            await _db.ExecuteAsync(
                "UPDATE dbo.venta SET estado = 'Anulado' WHERE idventa = @id",
                new { id = idVenta }, tx);

            var detalles = await _db.QueryAsync<DetalleStock>(
                "SELECT idarticulo, cantidad FROM dbo.detalle_venta WITH (NOLOCK) WHERE idventa = @id",
                new { id = idVenta }, tx);

            foreach (var det in detalles)
            {
                await _db.ExecuteAsync(
                    @"UPDATE dbo.sucursalArticulo
                      SET stock = stock + @cantidad
                      WHERE idarticulo = @idArticulo AND idsucursal = @idSucursal",
                    new { det.Cantidad, det.IdArticulo, idSucursal }, tx);
            }

            await _db.ExecuteAsync(
                @"INSERT INTO dbo.AnulacionVenta (IdVenta, IdUsuario, IdSucursal, Motivo)
                  VALUES (@idVenta, @idUsuario, @idSucursal, @motivo)",
                new { idVenta, dto.IdUsuario, idSucursal, dto.Motivo }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    // ──────────────────────────────────────────────────────────────
    // BUSCAR FACTURAS (paginado, filtros generales)
    // ──────────────────────────────────────────────────────────────
    public async Task<BuscarVentasResponseDto> BuscarAsync(BuscarVentasQueryDto filtro)
    {
        filtro.TamanoPagina = Math.Clamp(filtro.TamanoPagina, 1, 100);
        filtro.Pagina = Math.Max(filtro.Pagina, 1);
        var offset = (filtro.Pagina - 1) * filtro.TamanoPagina;

        const string sql = """
            SELECT
                v.idventa          AS IdVenta,
                v.CodigoFactura,
                ISNULL(NULLIF(LTRIM(RTRIM(v.NombreCliente)), ''), p.nombre) AS Cliente,
                u.nombre           AS Usuario,
                s.nombre           AS Sucursal,
                v.fecha_hora       AS FechaHora,
                v.estado           AS Estado,
                v.tipo_comprobante AS TipoComprobante,
                v.TipoPago,
                v.total            AS Total,
                COUNT(*) OVER()    AS TotalRegistros
            FROM dbo.venta v WITH (NOLOCK)
            INNER JOIN dbo.persona  p WITH (NOLOCK) ON p.idpersona  = v.idcliente
            INNER JOIN dbo.usuario  u WITH (NOLOCK) ON u.idusuario  = v.idusuario
            INNER JOIN dbo.sucursal s WITH (NOLOCK) ON s.idsucursal = u.idsucursal
            WHERE 1=1
                AND (@codigoFactura   IS NULL OR v.CodigoFactura    LIKE '%' + @codigoFactura   + '%')
                AND (@cliente         IS NULL OR ISNULL(NULLIF(LTRIM(RTRIM(v.NombreCliente)), ''), p.nombre) LIKE '%' + @cliente + '%')
                AND (@fechaDesde      IS NULL OR CAST(v.fecha_hora AS DATE) >= @fechaDesde)
                AND (@fechaHasta      IS NULL OR CAST(v.fecha_hora AS DATE) <= @fechaHasta)
                AND (@estado          IS NULL OR v.estado            = @estado)
                AND (@tipoComprobante IS NULL OR v.tipo_comprobante  = @tipoComprobante)
                AND (@sucursalId      IS NULL OR u.idsucursal        = @sucursalId)
            ORDER BY v.fecha_hora DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
            """;

        var rows = await _db.QueryAsync<VentaResumenDto>(sql, new
        {
            filtro.CodigoFactura,
            filtro.Cliente,
            filtro.FechaDesde,
            filtro.FechaHasta,
            filtro.Estado,
            filtro.TipoComprobante,
            filtro.SucursalId,
            offset,
            pageSize = filtro.TamanoPagina
        });

        var lista = rows.ToList();

        return new BuscarVentasResponseDto
        {
            Total = lista.FirstOrDefault()?.TotalRegistros ?? 0,
            Pagina = filtro.Pagina,
            TamanoPagina = filtro.TamanoPagina,
            Datos = lista
        };
    }

    // ──────────────────────────────────────────────────────────────
    // DETALLE DE UNA FACTURA
    // ──────────────────────────────────────────────────────────────
    public async Task<VentaDetalleDto> ObtenerDetalleAsync(int idVenta)
    {
        const string sqlHeader = """
            SELECT
                v.idventa          AS IdVenta,
                v.CodigoFactura,
                ISNULL(NULLIF(LTRIM(RTRIM(v.NombreCliente)), ''), p.nombre) AS Cliente,
                u.nombre           AS Usuario,
                s.nombre           AS Sucursal,
                v.fecha_hora       AS FechaHora,
                v.estado           AS Estado,
                v.tipo_comprobante AS TipoComprobante,
                v.TipoPago,
                v.DescripcionPago,
                v.total            AS Total,
                av.Motivo          AS MotivoAnulacion,
                u2.nombre          AS UsuarioAnulacion,
                av.FechaAnulacion
            FROM dbo.venta v WITH (NOLOCK)
            INNER JOIN dbo.persona  p  WITH (NOLOCK) ON p.idpersona  = v.idcliente
            INNER JOIN dbo.usuario  u  WITH (NOLOCK) ON u.idusuario  = v.idusuario
            INNER JOIN dbo.sucursal s  WITH (NOLOCK) ON s.idsucursal = u.idsucursal
            LEFT  JOIN dbo.AnulacionVenta av WITH (NOLOCK) ON av.IdVenta  = v.idventa
            LEFT  JOIN dbo.usuario  u2 WITH (NOLOCK) ON u2.idusuario = av.IdUsuario
            WHERE v.idventa = @idVenta;
            """;

        const string sqlItems = """
            SELECT
                dv.iddetalle_venta                        AS IdDetalleVenta,
                a.codigo                                   AS CodigoArticulo,
                a.nombre                                   AS Articulo,
                dv.cantidad                                AS Cantidad,
                dv.precio                                  AS Precio,
                dv.descuento                               AS Descuento,
                (dv.precio - dv.descuento) * dv.cantidad   AS Subtotal
            FROM dbo.detalle_venta dv WITH (NOLOCK)
            INNER JOIN dbo.articulo a WITH (NOLOCK) ON a.idarticulo = dv.idarticulo
            WHERE dv.idventa = @idVenta
            ORDER BY dv.iddetalle_venta;
            """;

        using var multi = await _db.QueryMultipleAsync(sqlHeader + sqlItems, new { idVenta });

        var header = await multi.ReadFirstOrDefaultAsync<VentaDetalleDto>()
            ?? throw new KeyNotFoundException($"La venta {idVenta} no existe.");

        header.Items = await multi.ReadAsync<VentaDetalleItemDto>();
        return header;
    }

    // ──────────────────────────────────────────────────────────────
    // BUSCAR ARTÍCULOS por código o nombre
    // ──────────────────────────────────────────────────────────────
    public async Task<IEnumerable<ArticuloResumenDto>> BuscarArticulosAsync(string termino)
    {
        const string sql = """
            SELECT TOP 50
                idarticulo  AS IdArticulo,
                codigo      AS Codigo,
                nombre      AS Nombre,
                precio_venta  AS PrecioVenta,
                precio_compra AS PrecioCompra,
                condicion   AS Condicion
            FROM dbo.articulo WITH (NOLOCK)
            WHERE condicion = 1
              AND (codigo LIKE '%' + @termino + '%'
                OR nombre LIKE '%' + @termino + '%')
            ORDER BY nombre
            """;

        return await _db.QueryAsync<ArticuloResumenDto>(sql, new { termino });
    }

    // ──────────────────────────────────────────────────────────────
    // BUSCAR FACTURAS POR ARTÍCULOS + CLIENTE (con devoluciones)
    // ──────────────────────────────────────────────────────────────
    public async Task<IEnumerable<FacturaPorProductoDto>> BuscarFacturasPorArticulosAsync(
        int[] idsArticulos, int idCliente)
    {
        if (idsArticulos.Length == 0)
            return [];

        // 1. Facturas del cliente que contienen alguno de los artículos
        const string sqlFacturas = """
            SELECT DISTINCT
                v.idventa          AS IdVenta,
                v.CodigoFactura,
                ISNULL(NULLIF(LTRIM(RTRIM(v.NombreCliente)), ''), p.nombre) AS Cliente,
                v.fecha_hora       AS FechaHora,
                v.estado           AS Estado,
                v.tipo_comprobante AS TipoComprobante,
                v.total            AS Total
            FROM dbo.venta v WITH (NOLOCK)
            INNER JOIN dbo.persona      p  WITH (NOLOCK) ON p.idpersona = v.idcliente
            INNER JOIN dbo.detalle_venta dv WITH (NOLOCK) ON dv.idventa  = v.idventa
            WHERE v.idcliente     = @idCliente
              AND dv.idarticulo  IN @idsArticulos
            ORDER BY v.fecha_hora DESC;
            """;

        // 2. Líneas de esos artículos en esas facturas
        const string sqlProductos = """
            SELECT
                dv.idventa    AS IdVenta,
                dv.idarticulo AS IdArticulo,
                a.codigo      AS Codigo,
                a.nombre      AS Nombre,
                dv.cantidad   AS Cantidad,
                dv.precio     AS Precio,
                dv.descuento  AS Descuento
            FROM dbo.detalle_venta dv WITH (NOLOCK)
            INNER JOIN dbo.articulo a WITH (NOLOCK) ON a.idarticulo = dv.idarticulo
            WHERE dv.idarticulo IN @idsArticulos
              AND dv.idventa IN (
                  SELECT DISTINCT v.idventa
                  FROM dbo.venta v WITH (NOLOCK)
                  INNER JOIN dbo.detalle_venta d WITH (NOLOCK) ON d.idventa = v.idventa
                  WHERE v.idcliente = @idCliente AND d.idarticulo IN @idsArticulos
              );
            """;

        // 3. Devoluciones por producto en esas facturas
        const string sqlDevoluciones = """
            SELECT
                nc.IdVenta,
                dn.IdProducto      AS IdArticulo,
                nc.Codigo          AS CodigoNota,
                nc.TipoNotaCredito,
                nc.Observaciones,
                nc.FechaCreacion,
                nc.estado          AS EstadoNota,
                u.nombre           AS UsuarioDevolucion,
                dn.Cantidad,
                dn.Precio,
                dn.Descuento,
                dn.Total
            FROM dbo.NotaCredito nc WITH (NOLOCK)
            INNER JOIN dbo.DetalleNotaCredito dn WITH (NOLOCK) ON dn.IdNotaCredito = nc.IdNotaCredito
            LEFT  JOIN dbo.usuario u WITH (NOLOCK) ON u.idusuario = nc.IdUsuario
            WHERE dn.IdProducto IN @idsArticulos
              AND nc.IdVenta IN (
                  SELECT DISTINCT v.idventa
                  FROM dbo.venta v WITH (NOLOCK)
                  INNER JOIN dbo.detalle_venta d WITH (NOLOCK) ON d.idventa = v.idventa
                  WHERE v.idcliente = @idCliente AND d.idarticulo IN @idsArticulos
              );
            """;

        var param = new { idsArticulos, idCliente };

        using var multi = await _db.QueryMultipleAsync(
            sqlFacturas + sqlProductos + sqlDevoluciones, param);

        var facturas   = (await multi.ReadAsync<FacturaPorProductoDto>()).ToList();
        var productos  = (await multi.ReadAsync<ProductoLineaRaw>()).ToList();
        var devoluciones = (await multi.ReadAsync<DevolucionLineaRaw>()).ToList();

        // Agrupar productos y devoluciones por factura
        var devPorFacturaYArticulo = devoluciones
            .GroupBy(d => (d.IdVenta, d.IdArticulo))
            .ToDictionary(g => g.Key, g => g.Select(d => new DevolucionProductoDto
            {
                CodigoNota       = d.CodigoNota,
                TipoNotaCredito  = d.TipoNotaCredito,
                Observaciones    = d.Observaciones,
                UsuarioDevolucion = d.UsuarioDevolucion,
                FechaCreacion    = d.FechaCreacion,
                EstadoNota       = d.EstadoNota,
                Cantidad         = d.Cantidad,
                Precio           = d.Precio,
                Descuento        = d.Descuento,
                Total            = d.Total
            }).ToList());

        var prodPorFactura = productos
            .GroupBy(p => p.IdVenta)
            .ToDictionary(g => g.Key, g => g.Select(p => new ProductoEnFacturaDto
            {
                IdArticulo  = p.IdArticulo,
                Codigo      = p.Codigo,
                Nombre      = p.Nombre,
                Cantidad    = p.Cantidad,
                Precio      = p.Precio,
                Descuento   = p.Descuento,
                Devoluciones = devPorFacturaYArticulo
                    .GetValueOrDefault((p.IdVenta, p.IdArticulo), [])
            }).ToList());

        foreach (var factura in facturas)
            factura.ProductosEncontrados = prodPorFactura.GetValueOrDefault(factura.IdVenta, []);

        return facturas;
    }

    // ──────────────────────────────────────────────────────────────
    // BUSCAR CLIENTES por nombre
    // ──────────────────────────────────────────────────────────────
    public async Task<IEnumerable<ClienteResumenDto>> BuscarClientesAsync(string termino)
    {
        const string sql = """
            SELECT TOP 30
                idpersona AS IdCliente,
                nombre    AS Nombre,
                telefono  AS Telefono,
                email     AS Email
            FROM dbo.persona WITH (NOLOCK)
            WHERE tipo_persona = 'Cliente'
              AND nombre LIKE '%' + @termino + '%'
            ORDER BY nombre
            """;
        return await _db.QueryAsync<ClienteResumenDto>(sql, new { termino });
    }

    // ── Tipos internos para mapeo Dapper ──────────────────────────
    private sealed record VentaAnulacion(int Idventa, string Estado, int? IdCredito, string CodigoFactura);
    private sealed record DetalleStock(int IdArticulo, int Cantidad);

    private sealed record ProductoLineaRaw(
        int IdVenta, int IdArticulo, string Codigo, string Nombre,
        int Cantidad, decimal Precio, decimal Descuento);

    private sealed record DevolucionLineaRaw(
        int IdVenta, int IdArticulo, string CodigoNota, string TipoNotaCredito,
        string? Observaciones, DateTime? FechaCreacion, string? EstadoNota,
        string? UsuarioDevolucion, int Cantidad, decimal Precio, decimal Descuento, decimal Total);
}
