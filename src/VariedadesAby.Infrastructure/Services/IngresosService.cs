using System.Data;
using Dapper;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VariedadesAby.Core.DTOs.Ingresos;
using VariedadesAby.Core.Exceptions; // IngresoException + NotFoundException
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class IngresosService : IIngresosService
{
    private readonly IDbConnection _db;

    public IngresosService(IDbConnection db)
    {
        _db = db;
    }

    public async Task<(int idIngreso, string numComprobante)> CrearAsync(CrearIngresoViewModel model)
    {
        // Fecha en zona horaria de Nicaragua (Central Standard Time)
        var fechaHora = TimeZoneInfo.ConvertTime(
            DateTime.Now,
            TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time"));

        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var transaction = _db.BeginTransaction();
        try
        {
            // ── 1. Verificar y crear/actualizar artículos ─────────────────────
            foreach (var detalle in model.detalles)
            {
                var idExistente = await _db.QueryFirstOrDefaultAsync<int?>(
                    "SELECT idarticulo FROM articulo WITH (NOLOCK) WHERE idarticulo = @id",
                    new { id = detalle.idarticulo }, transaction);

                // FAIL-SAFE: Si viene como nuevo (id=0), buscamos por código una última vez
                // para evitar crear duplicados bajo NINGUNA circunstancia.
                if (idExistente is null && !string.IsNullOrWhiteSpace(detalle.codigo))
                {
                    var idPorCodigo = await _db.QueryFirstOrDefaultAsync<int?>(
                        "SELECT idarticulo FROM articulo WITH (NOLOCK) WHERE RTRIM(codigo) = @codigo",
                        new { codigo = detalle.codigo.Trim() }, transaction);

                    if (idPorCodigo.HasValue)
                    {
                        idExistente = idPorCodigo;
                        detalle.idarticulo = idPorCodigo.Value;
                    }
                }

                if (idExistente is null)
                {
                    // Artículo nuevo → insertar y recuperar ID generado por la BD
                    var nuevoId = await _db.QuerySingleAsync<int>(
                        @"INSERT INTO articulo
                            (idcategoria, codigo, nombre, descripcion,
                             precio_compra, precio_venta, condicion, fecha_hora)
                          OUTPUT INSERTED.idarticulo
                          VALUES
                            (@idcategoria, @codigo, @nombre, @descripcion,
                             @precio_compra, @precio_venta, 1, @fecha_hora)",
                        new
                        {
                            idcategoria   = detalle.idCategoria ?? 1,
                            codigo        = detalle.codigo,
                            nombre        = detalle.nombreArticulo,
                            descripcion   = detalle.descripcionArticulo,
                            precio_compra = detalle.precio,
                            precio_venta  = detalle.precio_venta ?? 0,
                            fecha_hora    = fechaHora
                        }, transaction);

                    detalle.idarticulo = nuevoId;

                    // Inicializar stock en 0 para todas las sucursales
                    var sucursales = await _db.QueryAsync<int>(
                        "SELECT idsucursal FROM sucursal WITH (NOLOCK)",
                        transaction: transaction);

                    foreach (var idSucursal in sucursales)
                    {
                        await _db.ExecuteAsync(
                            @"INSERT INTO sucursalArticulo (idsucursal, idarticulo, stock)
                              VALUES (@idsucursal, @idarticulo, 0)",
                            new { idsucursal = idSucursal, idarticulo = nuevoId },
                            transaction);
                    }
                }
                else
                {
                    // Artículo existente → actualizar precios
                    await _db.ExecuteAsync(
                        @"UPDATE articulo
                             SET precio_compra = @precio_compra,
                                 precio_venta  = @precio_venta
                           WHERE idarticulo = @idarticulo",
                        new
                        {
                            precio_compra = detalle.precio,
                            precio_venta  = detalle.precio_venta ?? 0,
                            idarticulo    = detalle.idarticulo
                        }, transaction);
                }
            }

            // ── 2. Manejar crédito si aplica ─────────────────────────────────
            int? idCredito = null;

            if (model.tipo_comprobante.Equals("CREDITO", StringComparison.OrdinalIgnoreCase))
            {
                idCredito = await _db.QueryFirstOrDefaultAsync<int?>(
                    @"SELECT Id_Credito FROM credito WITH (NOLOCK)
                       WHERE Id_Estado = 1 AND Id_Persona = @idpersona",
                    new { idpersona = model.idproveedor }, transaction);

                if (idCredito is null)
                {
                    idCredito = await _db.QuerySingleAsync<int>(
                        @"INSERT INTO credito (Id_Estado, Id_Persona, PrimerCredito)
                          OUTPUT INSERTED.Id_Credito
                          VALUES (1, @idpersona, @fecha)",
                        new { idpersona = model.idproveedor, fecha = fechaHora },
                        transaction);

                    await _db.ExecuteAsync(
                        "UPDATE persona SET Id_Credito = @idCredito WHERE idpersona = @idpersona",
                        new { idCredito, idpersona = model.idproveedor },
                        transaction);
                }
            }

            // ── 3. Insertar ingreso ───────────────────────────────────────────
            var estado = model.tipo_comprobante.Equals("CONTADO", StringComparison.OrdinalIgnoreCase)
                ? "CANCELADO"
                : "PENDIENTE";

            var idIngreso = await _db.QuerySingleAsync<int>(
                @"INSERT INTO ingreso
                    (idproveedor, idusuario, tipo_comprobante, serie_comprobante,
                     num_comprobante, fecha_hora, impuesto, total, estado, IdSucursal, IdCredito)
                  OUTPUT INSERTED.idingreso
                  VALUES
                    (@idproveedor, @idusuario, @tipo_comprobante, @serie_comprobante,
                     @num_comprobante, @fecha_hora, @impuesto, @total, @estado, @idSucursal, @idCredito)",
                new
                {
                    model.idproveedor,
                    model.idusuario,
                    model.tipo_comprobante,
                    model.serie_comprobante,
                    model.num_comprobante,
                    fecha_hora = fechaHora,
                    model.impuesto,
                    model.total,
                    estado,
                    idSucursal = model.idSucursal == 0 ? 1 : model.idSucursal,
                    idCredito
                }, transaction);

            // ── 4. Actualizar stock y registrar detalles ──────────────────────
            int idSucursalEfectivo = model.idSucursal == 0 ? 1 : model.idSucursal;

            foreach (var detalle in model.detalles)
            {
                // Stock
                var stockExiste = await _db.QueryFirstOrDefaultAsync<int?>(
                    @"SELECT 1 FROM sucursalArticulo WITH (NOLOCK)
                       WHERE idarticulo = @idarticulo AND idsucursal = @idsucursal",
                    new { idarticulo = detalle.idarticulo, idsucursal = idSucursalEfectivo },
                    transaction);

                if (stockExiste is null)
                {
                    await _db.ExecuteAsync(
                        @"INSERT INTO sucursalArticulo (idsucursal, idarticulo, stock)
                          VALUES (@idsucursal, @idarticulo, @stock)",
                        new { idsucursal = idSucursalEfectivo, idarticulo = detalle.idarticulo, stock = detalle.cantidad },
                        transaction);
                }
                else
                {
                    await _db.ExecuteAsync(
                        @"UPDATE sucursalArticulo
                             SET stock = stock + @cantidad
                           WHERE idarticulo = @idarticulo AND idsucursal = @idsucursal",
                        new { detalle.cantidad, idarticulo = detalle.idarticulo, idsucursal = idSucursalEfectivo },
                        transaction);
                }

                // Detalle
                await _db.ExecuteAsync(
                    @"INSERT INTO detalle_ingreso (idingreso, idarticulo, cantidad, precio, precio_venta)
                      VALUES (@idingreso, @idarticulo, @cantidad, @precio, @precio_venta)",
                    new
                    {
                        idingreso    = idIngreso,
                        idarticulo   = detalle.idarticulo,
                        detalle.cantidad,
                        detalle.precio,
                        precio_venta = detalle.precio_venta ?? 0
                    }, transaction);
            }

            // ── 5. Insertar URLs de imágenes del comprobante ──────────────────
            if (model.urlsImagenes != null && model.urlsImagenes.Any())
            {
                foreach (var url in model.urlsImagenes)
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        await _db.ExecuteAsync(
                            @"INSERT INTO ingreso_imagen (idingreso, url_imagen)
                              VALUES (@idingreso, @url)",
                            new { idingreso = idIngreso, url = url.Trim() },
                            transaction);
                    }
                }
            }

            transaction.Commit();
            return (idIngreso, model.num_comprobante);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<bool> AnularAsync(int idIngreso)
    {
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var transaction = _db.BeginTransaction();
        try
        {
            var ingreso = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT estado, IdSucursal FROM ingreso WITH (UPDLOCK) WHERE idingreso = @id",
                new { id = idIngreso }, transaction);

            if (ingreso == null)
                throw new NotFoundException($"No se encontró la compra con ID {idIngreso}");

            if (((string)ingreso.estado).Equals("ANULADO", StringComparison.OrdinalIgnoreCase))
                throw new IngresoException("La compra ya se encuentra anulada.");

            var idSucursalEfectivo = (int)(ingreso.IdSucursal ?? 1);
            if (idSucursalEfectivo == 0) idSucursalEfectivo = 1;

            var detalles = await _db.QueryAsync<dynamic>(
                @"SELECT d.idarticulo, d.cantidad, a.nombre, ISNULL(s.stock, 0) as stockActual
                  FROM detalle_ingreso d WITH (NOLOCK)
                  INNER JOIN articulo a WITH (NOLOCK) ON a.idarticulo = d.idarticulo
                  LEFT JOIN sucursalArticulo s WITH (UPDLOCK) ON s.idarticulo = d.idarticulo AND s.idsucursal = @idSucursal
                  WHERE d.idingreso = @idIngreso",
                new { idIngreso, idSucursal = idSucursalEfectivo }, transaction);

            // 1. Validar estrictamente el stock
            foreach (var detalle in detalles)
            {
                if (detalle.stockActual < detalle.cantidad)
                {
                    throw new IngresoException($"No se puede anular. El artículo '{detalle.nombre}' ingresó {detalle.cantidad} unidades, pero el stock actual en esta sucursal es solo de {detalle.stockActual}. Ya tiene ventas o traslados.");
                }
            }

            // 2. Restar stock
            foreach (var detalle in detalles)
            {
                await _db.ExecuteAsync(
                    @"UPDATE sucursalArticulo
                         SET stock = stock - @cantidad
                       WHERE idarticulo = @idarticulo AND idsucursal = @idsucursal",
                    new { cantidad = (int)detalle.cantidad, idarticulo = (int)detalle.idarticulo, idsucursal = idSucursalEfectivo },
                    transaction);
            }

            // 3. Cambiar estado a ANULADO
            await _db.ExecuteAsync(
                "UPDATE ingreso SET estado = 'ANULADO' WHERE idingreso = @id",
                new { id = idIngreso }, transaction);

            transaction.Commit();
            return true;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<PagedResult<AnalisisInventarioProveedorDto>> AnalisisInventarioPorProveedorAsync(
        FiltroAnalisisProveedorViewModel filtro)
    {
        filtro.pagina    = Math.Max(filtro.pagina, 1);
        filtro.porPagina = Math.Clamp(filtro.porPagina, 1, 100);

        var offset = (filtro.pagina - 1) * filtro.porPagina;
        var p      = new DynamicParameters();

        var whereProveedor = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtro.busqueda))
        {
            whereProveedor = "AND p.nombre LIKE @busqueda";
            p.Add("busqueda", $"%{filtro.busqueda.Trim()}%");
        }

        var ctes = @"
            WITH StockPorArticulo AS (
                SELECT idarticulo, SUM(ISNULL(stock, 0)) AS stockTotal
                FROM sucursalArticulo WITH (NOLOCK)
                GROUP BY idarticulo
            ),
            VentasPorArticulo AS (
                SELECT dv.idarticulo, SUM(dv.cantidad) AS totalVendido
                FROM detalle_venta dv WITH (NOLOCK)
                INNER JOIN venta v WITH (NOLOCK) ON v.idventa = dv.idventa
                                                AND v.estado <> 'Anulado'
                GROUP BY dv.idarticulo
            ),
            UltimaVentaPorArticulo AS (
                SELECT dv.idarticulo, MAX(v.fecha_hora) AS ultimaVenta
                FROM detalle_venta dv WITH (NOLOCK)
                INNER JOIN venta v WITH (NOLOCK) ON v.idventa = dv.idventa
                                                AND v.estado <> 'Anulado'
                GROUP BY dv.idarticulo
            ),
            ComprasPorProveedorArticulo AS (
                SELECT
                    i.idproveedor,
                    di.idarticulo,
                    SUM(di.cantidad)             AS totalComprado,
                    SUM(di.cantidad * di.precio) AS costoTotal,
                    AVG(di.precio)               AS precioPromedio,
                    MAX(i.fecha_hora)            AS ultimaCompraArticulo
                FROM detalle_ingreso di WITH (NOLOCK)
                INNER JOIN ingreso i WITH (NOLOCK) ON i.idingreso = di.idingreso
                                                  AND i.estado <> 'ANULADO'
                GROUP BY i.idproveedor, di.idarticulo
            )";

        var sqlCount = $@"
            {ctes}
            SELECT COUNT(DISTINCT p.idpersona)
            FROM persona p WITH (NOLOCK)
            INNER JOIN ComprasPorProveedorArticulo c ON c.idproveedor = p.idpersona
            INNER JOIN ingreso i WITH (NOLOCK) ON i.idproveedor = p.idpersona
                                              AND i.estado <> 'ANULADO'
            WHERE p.idpersona != 13686 {whereProveedor}";

        var sqlData = $@"
            {ctes}
            SELECT
                p.idpersona                                                    AS idproveedor,
                p.nombre                                                       AS proveedor,
                COUNT(DISTINCT c.idarticulo)                                   AS totalProductos,
                COUNT(DISTINCT CASE WHEN ISNULL(s.stockTotal,0) > 0
                                    THEN c.idarticulo END)                     AS productosConStock,
                SUM(c.totalComprado)                                           AS totalUnidadesCompradas,
                SUM(ISNULL(s.stockTotal, 0))                                   AS stockActualUnidades,
                SUM(ISNULL(v.totalVendido, 0))                                 AS unidadesVendidas,
                ROUND(SUM(c.costoTotal), 2)                                    AS totalInvertido,
                ROUND(SUM(ISNULL(s.stockTotal, 0) * c.precioPromedio), 2)     AS valorStockActual,
                ROUND(SUM(ISNULL(v.totalVendido, 0) * c.precioPromedio), 2)   AS valorRecuperado,
                ROUND(
                    CASE WHEN SUM(c.totalComprado) > 0
                    THEN SUM(ISNULL(v.totalVendido, 0)) * 100.0 / SUM(c.totalComprado)
                    ELSE 0 END, 1)                                             AS porcentajeRotacion,
                MAX(i.fecha_hora)                                              AS ultimaCompra,
                AVG(DATEDIFF(DAY, COALESCE(uv.ultimaVenta, c.ultimaCompraArticulo), GETDATE()))
                                                                               AS diasSinMovimientoPromedio
            FROM persona p WITH (NOLOCK)
            INNER JOIN ComprasPorProveedorArticulo c ON c.idproveedor = p.idpersona
            INNER JOIN ingreso i WITH (NOLOCK) ON i.idproveedor = p.idpersona
                                              AND i.estado <> 'ANULADO'
            LEFT  JOIN StockPorArticulo         s  ON s.idarticulo  = c.idarticulo
            LEFT  JOIN VentasPorArticulo        v  ON v.idarticulo  = c.idarticulo
            LEFT  JOIN UltimaVentaPorArticulo   uv ON uv.idarticulo = c.idarticulo
            WHERE p.idpersona != 13686 {whereProveedor}
            GROUP BY p.idpersona, p.nombre
            ORDER BY valorStockActual DESC
            OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY";

        p.Add("offset",    offset);
        p.Add("porPagina", filtro.porPagina);

        var total = await _db.ExecuteScalarAsync<int>(sqlCount, p);
        var raw   = await _db.QueryAsync<AnalisisInventarioProveedorDto>(sqlData, p);

        foreach (var r in raw)
        {
            (r.clasificacionInventario, r.semaforo) = r.diasSinMovimientoPromedio switch
            {
                <= 15 => ("Fresco",  "verde"),
                <= 45 => ("Lento",   "amarillo"),
                <= 90 => ("Dormido", "rojo"),
                _     => ("Muerto",  "negro")
            };

            r.recomendacion = (r.diasSinMovimientoPromedio, r.porcentajeRotacion) switch
            {
                (<= 15, _)     => "Inventario fresco — Compra reciente, aún en periodo de venta normal.",
                (<= 45, >= 60) => "Buena rotación — Productos moviéndose a buen ritmo.",
                (<= 45, _)     => "Rotación media — Evaluar antes de hacer un nuevo pedido.",
                (<= 90, >= 60) => "Rotación aceptable pero lenta — Considerar promociones.",
                (<= 90, _)     => "Inventario dormido — Stock elevado sin vender, precaución al invertir.",
                (_, >= 60)     => "Inventario muerto con algo de rotación — Liquidar stock restante.",
                _              => "Inventario muerto — No invertir más, liquidar existencias."
            };
        }

        return new PagedResult<AnalisisInventarioProveedorDto>
        {
            data           = raw,
            totalRegistros = total,
            pagina         = filtro.pagina,
            porPagina      = filtro.porPagina
        };
    }

    public async Task<PagedResult<AnalisisProveedorDetalleDto>> AnalisisDetalleProveedorAsync(
        int idProveedor, FiltroAnalisisProveedorViewModel filtro)
    {
        filtro.pagina    = Math.Max(filtro.pagina, 1);
        filtro.porPagina = Math.Clamp(filtro.porPagina, 1, 100);

        var offset = (filtro.pagina - 1) * filtro.porPagina;
        var p      = new DynamicParameters();
        p.Add("idProveedor", idProveedor);

        var whereArticulo = string.Empty;
        if (!string.IsNullOrWhiteSpace(filtro.busqueda))
        {
            whereArticulo = "AND a.nombre LIKE @busqueda";
            p.Add("busqueda", $"%{filtro.busqueda.Trim()}%");
        }

        var ctes = @"
            WITH StockPorArticulo AS (
                SELECT idarticulo, SUM(ISNULL(stock, 0)) AS stockTotal
                FROM sucursalArticulo WITH (NOLOCK)
                GROUP BY idarticulo
            ),
            VentasPorArticulo AS (
                SELECT dv.idarticulo, SUM(dv.cantidad) AS totalVendido
                FROM detalle_venta dv WITH (NOLOCK)
                INNER JOIN venta v WITH (NOLOCK) ON v.idventa = dv.idventa
                                                AND v.estado <> 'Anulado'
                GROUP BY dv.idarticulo
            ),
            UltimaVentaPorArticulo AS (
                SELECT dv.idarticulo, MAX(v.fecha_hora) AS ultimaVenta
                FROM detalle_venta dv WITH (NOLOCK)
                INNER JOIN venta v WITH (NOLOCK) ON v.idventa = dv.idventa
                                                AND v.estado <> 'Anulado'
                GROUP BY dv.idarticulo
            ),
            ComprasArticulo AS (
                SELECT
                    di.idarticulo,
                    SUM(di.cantidad)             AS totalComprado,
                    SUM(di.cantidad * di.precio) AS costoTotal,
                    AVG(di.precio)               AS precioPromedio,
                    MAX(i.fecha_hora)            AS ultimaCompra
                FROM detalle_ingreso di WITH (NOLOCK)
                INNER JOIN ingreso i WITH (NOLOCK) ON i.idingreso = di.idingreso
                                                  AND i.estado <> 'ANULADO'
                WHERE i.idproveedor = @idProveedor
                GROUP BY di.idarticulo
            )";

        var sqlCount = $@"
            {ctes}
            SELECT COUNT(*)
            FROM ComprasArticulo c
            INNER JOIN articulo a WITH (NOLOCK) ON a.idarticulo = c.idarticulo
            WHERE 1=1 {whereArticulo}";

        var sqlData = $@"
            {ctes}
            SELECT
                a.codigo,
                a.nombre                                                              AS articulo,
                cat.nombre                                                            AS categoria,
                c.totalComprado,
                ISNULL(s.stockTotal, 0)                                               AS stockActual,
                ISNULL(v.totalVendido, 0)                                             AS unidadesVendidas,
                ROUND(
                    CASE WHEN c.totalComprado > 0
                    THEN ISNULL(v.totalVendido, 0) * 100.0 / c.totalComprado
                    ELSE 0 END, 1)                                                    AS porcentajeRotacion,
                c.precioPromedio                                                      AS precioCompra,
                a.precio_venta                                                        AS precioVenta,
                ROUND(ISNULL(s.stockTotal, 0) * c.precioPromedio, 2)                 AS valorStockActual,
                ROUND(c.costoTotal, 2)                                                AS totalInvertido,
                c.ultimaCompra,
                uv.ultimaVenta,
                DATEDIFF(DAY, COALESCE(uv.ultimaVenta, c.ultimaCompra), GETDATE())   AS diasSinMovimiento
            FROM ComprasArticulo c
            INNER JOIN articulo  a   WITH (NOLOCK) ON a.idarticulo    = c.idarticulo
            LEFT  JOIN categoria cat WITH (NOLOCK) ON cat.idcategoria = a.idcategoria
            LEFT  JOIN StockPorArticulo      s  ON s.idarticulo  = c.idarticulo
            LEFT  JOIN VentasPorArticulo     v  ON v.idarticulo  = c.idarticulo
            LEFT  JOIN UltimaVentaPorArticulo uv ON uv.idarticulo = c.idarticulo
            WHERE 1=1 {whereArticulo}
            ORDER BY diasSinMovimiento DESC, valorStockActual DESC
            OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY";

        p.Add("offset",    offset);
        p.Add("porPagina", filtro.porPagina);

        var total = await _db.ExecuteScalarAsync<int>(sqlCount, p);
        var raw   = await _db.QueryAsync<AnalisisProveedorDetalleDto>(sqlData, p);

        foreach (var r in raw)
        {
            (r.clasificacionInventario, r.semaforo) = r.diasSinMovimiento switch
            {
                <= 15 => ("Fresco",  "verde"),
                <= 45 => ("Lento",   "amarillo"),
                <= 90 => ("Dormido", "rojo"),
                _     => ("Muerto",  "negro")
            };
        }

        return new PagedResult<AnalisisProveedorDetalleDto>
        {
            data           = raw,
            totalRegistros = total,
            pagina         = filtro.pagina,
            porPagina      = filtro.porPagina
        };
    }

    public async Task<IngresoDetalleDto> ObtenerDetalleAsync(int idIngreso)
    {
        var encabezado = await _db.QueryFirstOrDefaultAsync<IngresoDetalleDto>(
            @"SELECT
                i.idingreso,
                prov.nombre         AS proveedor,
                prov.telefono       AS telefonoProveedor,
                u.nombre            AS usuario,
                s.nombre            AS sucursal,
                i.tipo_comprobante,
                i.serie_comprobante,
                i.num_comprobante,
                i.fecha_hora,
                i.impuesto,
                i.total,
                i.estado,
                ROUND(i.total / (1 + i.impuesto), 2)          AS subtotal,
                ROUND(i.total - i.total / (1 + i.impuesto), 2) AS totalImpuesto
              FROM ingreso i WITH (NOLOCK)
              INNER JOIN persona  prov WITH (NOLOCK) ON prov.idpersona = i.idproveedor
              INNER JOIN usuario  u    WITH (NOLOCK) ON u.idusuario    = i.idusuario
              LEFT  JOIN sucursal s    WITH (NOLOCK) ON s.idsucursal   = i.IdSucursal
              WHERE i.idingreso = @idIngreso",
            new { idIngreso })
            ?? throw new NotFoundException($"No se encontró el ingreso con ID {idIngreso}.");

        encabezado.items = (await _db.QueryAsync<ItemIngresoDto>(
            @"SELECT
                d.iddetalle_ingreso  AS iddetalle,
                a.codigo,
                a.nombre             AS articulo,
                d.cantidad,
                d.precio,
                d.precio_venta,
                ROUND(d.cantidad * d.precio, 2) AS subtotal
              FROM detalle_ingreso d WITH (NOLOCK)
              INNER JOIN articulo a WITH (NOLOCK) ON a.idarticulo = d.idarticulo
              WHERE d.idingreso = @idIngreso
              ORDER BY d.iddetalle_ingreso",
            new { idIngreso })).ToList();

        encabezado.urlsImagenes = (await _db.QueryAsync<string>(
            "SELECT url_imagen FROM ingreso_imagen WITH (NOLOCK) WHERE idingreso = @idIngreso",
            new { idIngreso })).ToList();

        return encabezado;
    }

    public async Task<byte[]> GenerarPdfAsync(int idIngreso)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var detalle = await ObtenerDetalleAsync(idIngreso);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                // ── Header ───────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        // Empresa
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("VARIEDADES ABY")
                                .FontSize(20).Bold().FontColor("#1a237e");
                            c.Item().Text("Comprobante de Compra")
                                .FontSize(10).FontColor("#546e7a");
                        });

                        // Info del comprobante
                        row.ConstantItem(180).Border(1).BorderColor("#1a237e")
                            .Padding(8).Column(c =>
                            {
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("N° Comprobante:").Bold();
                                    r.RelativeItem().AlignRight()
                                        .Text($"{detalle.serie_comprobante}-{detalle.num_comprobante}".TrimStart('-'))
                                        .Bold().FontColor("#1a237e");
                                });
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Fecha:");
                                    r.RelativeItem().AlignRight()
                                        .Text(detalle.fecha_hora.ToString("dd/MM/yyyy HH:mm"));
                                });
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Tipo:");
                                    r.RelativeItem().AlignRight()
                                        .Text(detalle.tipo_comprobante)
                                        .FontColor(detalle.tipo_comprobante == "CREDITO" ? "#c62828" : "#2e7d32");
                                });
                                c.Item().Row(r =>
                                {
                                    r.RelativeItem().Text("Estado:");
                                    r.RelativeItem().AlignRight()
                                        .Text(detalle.estado)
                                        .FontColor(detalle.estado == "CANCELADO" ? "#2e7d32" : "#e65100");
                                });
                            });
                    });

                    col.Item().PaddingTop(6).LineHorizontal(2).LineColor("#1a237e");
                });

                // ── Content ──────────────────────────────────────────────────
                page.Content().PaddingTop(12).Column(col =>
                {
                    // Datos proveedor y sucursal
                    col.Item().PaddingBottom(12).Row(row =>
                    {
                        row.RelativeItem().Background("#f5f5f5").Padding(8).Column(c =>
                        {
                            c.Item().Text("PROVEEDOR").Bold().FontSize(8).FontColor("#546e7a");
                            c.Item().Text(detalle.proveedor).Bold().FontSize(11);
                            if (!string.IsNullOrWhiteSpace(detalle.telefonoProveedor))
                                c.Item().Text($"Tel: {detalle.telefonoProveedor}").FontSize(8);
                        });

                        row.ConstantItem(10);

                        row.RelativeItem().Background("#f5f5f5").Padding(8).Column(c =>
                        {
                            c.Item().Text("SUCURSAL").Bold().FontSize(8).FontColor("#546e7a");
                            c.Item().Text(detalle.sucursal).Bold().FontSize(11);
                            c.Item().Text($"Recibido por: {detalle.usuario}").FontSize(8);
                        });
                    });

                    // Tabla de artículos
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(25);   // #
                            cols.ConstantColumn(60);   // Código
                            cols.RelativeColumn();     // Artículo
                            cols.ConstantColumn(38);   // Cant.
                            cols.ConstantColumn(65);   // P. Compra
                            cols.ConstantColumn(65);   // P. Venta
                            cols.ConstantColumn(70);   // Subtotal
                        });

                        // Encabezados
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background("#1a237e").Padding(5).AlignCenter();

                        table.Cell().Element(HeaderCell).Text("#").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Element(HeaderCell).Text("Código").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Element(HeaderCell).Text("Artículo").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Element(HeaderCell).Text("Cant.").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Element(HeaderCell).Text("P. Compra").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Element(HeaderCell).Text("P. Venta").Bold().FontColor(Colors.White).FontSize(8);
                        table.Cell().Element(HeaderCell).Text("Subtotal").Bold().FontColor(Colors.White).FontSize(8);

                        // Filas
                        var i = 0;
                        foreach (var item in detalle.items)
                        {
                            i++;
                            var bg = i % 2 == 0 ? "#f5f5f5" : "#ffffff";

                            static IContainer DataCell(IContainer c, string bg) =>
                                c.Background(bg).BorderBottom(1).BorderColor("#e0e0e0").Padding(5);

                            table.Cell().Element(c => DataCell(c, bg)).AlignCenter()
                                .Text(i.ToString()).FontSize(8);
                            table.Cell().Element(c => DataCell(c, bg))
                                .Text(item.codigo).FontSize(8).FontColor("#546e7a");
                            table.Cell().Element(c => DataCell(c, bg))
                                .Text(item.articulo).FontSize(8);
                            table.Cell().Element(c => DataCell(c, bg)).AlignCenter()
                                .Text(item.cantidad.ToString()).FontSize(8);
                            table.Cell().Element(c => DataCell(c, bg)).AlignRight()
                                .Text($"C$ {item.precio:N2}").FontSize(8);
                            table.Cell().Element(c => DataCell(c, bg)).AlignRight()
                                .Text(item.precio_venta.HasValue ? $"C$ {item.precio_venta:N2}" : "-")
                                .FontSize(8).FontColor(item.precio_venta.HasValue ? "#000000" : "#9e9e9e");
                            table.Cell().Element(c => DataCell(c, bg)).AlignRight()
                                .Text($"C$ {item.subtotal:N2}").FontSize(8).Bold();
                        }
                    });

                    // Totales
                    col.Item().PaddingTop(10).AlignRight().Width(220).Column(c =>
                    {
                        c.Item().BorderBottom(1).BorderColor("#e0e0e0").PaddingBottom(4).Row(r =>
                        {
                            r.RelativeItem().Text("Subtotal:");
                            r.ConstantItem(100).AlignRight().Text($"C$ {detalle.subtotal:N2}");
                        });
                        c.Item().BorderBottom(1).BorderColor("#e0e0e0").PaddingVertical(4).Row(r =>
                        {
                            r.RelativeItem().Text($"IVA ({detalle.impuesto:P0}):");
                            r.ConstantItem(100).AlignRight().Text($"C$ {detalle.totalImpuesto:N2}");
                        });
                        c.Item().PaddingTop(4).Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL:").Bold().FontSize(12);
                            r.ConstantItem(100).AlignRight()
                                .Text($"C$ {detalle.total:N2}").Bold().FontSize(12).FontColor("#1a237e");
                        });
                    });
                });

                // ── Footer ───────────────────────────────────────────────────
                page.Footer().PaddingTop(8).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor("#e0e0e0");
                    col.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem()
                            .Text("Variedades Aby — Documento generado automáticamente")
                            .FontSize(7).FontColor("#9e9e9e");
                        row.ConstantItem(80).AlignRight().Text(t =>
                        {
                            t.Span("Página ").FontSize(7).FontColor("#9e9e9e");
                            t.CurrentPageNumber().FontSize(7).FontColor("#9e9e9e");
                            t.Span(" de ").FontSize(7).FontColor("#9e9e9e");
                            t.TotalPages().FontSize(7).FontColor("#9e9e9e");
                        });
                    });
                });
            });
        }).GeneratePdf();
    }

    public async Task<PagedResult<IngresoListadoDto>> ListarAsync(FiltroIngresosViewModel filtro)
    {
        filtro.pagina    = Math.Max(filtro.pagina, 1);
        filtro.porPagina = Math.Clamp(filtro.porPagina, 1, 100);

        var offset = (filtro.pagina - 1) * filtro.porPagina;

        var where = new List<string>();
        var p = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(filtro.busqueda))
        {
            where.Add("(i.num_comprobante LIKE @busqueda OR prov.nombre LIKE @busqueda)");
            p.Add("busqueda", $"%{filtro.busqueda.Trim()}%");
        }
        if (!string.IsNullOrWhiteSpace(filtro.estado))
        {
            where.Add("i.estado = @estado");
            p.Add("estado", filtro.estado.ToUpper());
        }
        if (filtro.idSucursal.HasValue)
        {
            where.Add("i.IdSucursal = @idSucursal");
            p.Add("idSucursal", filtro.idSucursal);
        }
        if (filtro.idProveedor.HasValue)
        {
            where.Add("i.idproveedor = @idProveedor");
            p.Add("idProveedor", filtro.idProveedor);
        }
        if (filtro.fechaDesde.HasValue)
        {
            where.Add("i.fecha_hora >= @fechaDesde");
            p.Add("fechaDesde", filtro.fechaDesde.Value.Date);
        }
        if (filtro.fechaHasta.HasValue)
        {
            where.Add("i.fecha_hora < @fechaHasta");
            p.Add("fechaHasta", filtro.fechaHasta.Value.Date.AddDays(1));
        }

        var clausulaWhere = where.Count > 0
            ? "WHERE " + string.Join(" AND ", where)
            : string.Empty;

        var sqlData = $@"
            SELECT
                i.idingreso,
                prov.nombre   AS proveedor,
                u.nombre      AS usuario,
                s.nombre      AS sucursal,
                i.tipo_comprobante,
                i.serie_comprobante,
                i.num_comprobante,
                i.fecha_hora,
                i.impuesto,
                i.total,
                i.estado,
                COUNT(d.iddetalle_ingreso) AS totalArticulos
            FROM ingreso i WITH (NOLOCK)
            INNER JOIN persona  prov WITH (NOLOCK) ON prov.idpersona  = i.idproveedor
            INNER JOIN usuario  u    WITH (NOLOCK) ON u.idusuario     = i.idusuario
            LEFT  JOIN sucursal s    WITH (NOLOCK) ON s.idsucursal    = i.IdSucursal
            LEFT  JOIN detalle_ingreso d WITH (NOLOCK) ON d.idingreso = i.idingreso
            {clausulaWhere}
            GROUP BY
                i.idingreso, prov.nombre, u.nombre, s.nombre,
                i.tipo_comprobante, i.serie_comprobante, i.num_comprobante,
                i.fecha_hora, i.impuesto, i.total, i.estado
            ORDER BY i.idingreso DESC
            OFFSET @offset ROWS FETCH NEXT @porPagina ROWS ONLY";

        var sqlCount = $@"
            SELECT COUNT(DISTINCT i.idingreso)
            FROM ingreso i WITH (NOLOCK)
            INNER JOIN persona prov WITH (NOLOCK) ON prov.idpersona = i.idproveedor
            {clausulaWhere}";

        p.Add("offset",    offset);
        p.Add("porPagina", filtro.porPagina);

        var data  = await _db.QueryAsync<IngresoListadoDto>(sqlData, p);
        var total = await _db.ExecuteScalarAsync<int>(sqlCount, p);

        return new PagedResult<IngresoListadoDto>
        {
            data           = data,
            totalRegistros = total,
            pagina         = filtro.pagina,
            porPagina      = filtro.porPagina
        };
    }
}
