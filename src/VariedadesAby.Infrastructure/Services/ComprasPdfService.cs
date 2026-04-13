using System.Data;
using System.Text.Json;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Dapper;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;
using VariedadesAby.Core.DTOs.Compras;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class ComprasPdfService : IComprasPdfService
{
    private readonly IDbConnection _dbConnection;
    private readonly Client _geminiClient;
    private readonly string _modelo;
    private readonly string _modeloFallback;
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<ComprasPdfService> _logger;

    private const int MAX_RETRIES = 3;
    private const int INITIAL_DELAY_MS = 3000;

    public ComprasPdfService(
        IDbConnection dbConnection,
        IConfiguration configuration,
        ILogger<ComprasPdfService> logger)
    {
        _dbConnection = dbConnection;
        _logger = logger;

        var apiKey = configuration["GeminiAI:Key"]
            ?? throw new InvalidOperationException("GeminiAI:Key no está configurada.");
        _modelo = configuration["GeminiAI:ModeloPdf"]
            ?? configuration["GeminiAI:Modelo"]
            ?? "gemini-2.5-pro";
        _modeloFallback = configuration["GeminiAI:ModeloFallback"]
            ?? "gemini-2.0-flash";
        _geminiClient = new Client(apiKey: apiKey, httpOptions: new HttpOptions
        {
            Timeout = (int)TimeSpan.FromMinutes(5).TotalMilliseconds
        });

        _cloudinary = new Cloudinary(new Account(
            configuration["Cloudinary:CloudName"]
                ?? throw new InvalidOperationException("Cloudinary:CloudName no configurado."),
            configuration["Cloudinary:ApiKey"]
                ?? throw new InvalidOperationException("Cloudinary:ApiKey no configurado."),
            configuration["Cloudinary:ApiSecret"]
                ?? throw new InvalidOperationException("Cloudinary:ApiSecret no configurado.")
        ));
    }

    public async Task<AnalizarPdfResponseDto> AnalizarPdfAsync(
        Stream pdfStream, int idProveedor, int idUsuario, int idSucursal)
    {
        // 1. Leer bytes del PDF
        using var ms = new MemoryStream();
        await pdfStream.CopyToAsync(ms);
        var pdfBytes = ms.ToArray();

        // 2. Convertir páginas del PDF a imágenes JPEG
        var imagenesBytes = ConvertirPdfAImagenes(pdfBytes);

        if (imagenesBytes.Count == 0)
            throw new InvalidOperationException("No se pudieron extraer imágenes del PDF.");

        // 3 + 4. Cloudinary y Gemini en paralelo — ahorran el tiempo de subida
        var cloudinaryTask = SubirImagenesACloudinaryAsync(imagenesBytes);
        var geminiTask     = EjecutarConReintentoAsync((modelo) => ExtraerDatosConGeminiAsync(pdfBytes, modelo));

        await Task.WhenAll(cloudinaryTask, geminiTask);

        var urlsCloudinary = cloudinaryTask.Result;
        var (resultado, modeloUsado) = geminiTask.Result;
        var (compraExtraida, proveedorDetectado) = resultado;

        // 4.1 Agrupar productos repetidos (mismo código y precio) para consolidar cantidades
        compraExtraida.detalles = AgruparDetalles(compraExtraida.detalles);

        // 4.2 Validar coherencia numérica antes de aplicar tipo de cambio
        var advertencias = ValidarCoherenciaTotales(compraExtraida);

        // 5. Validar cada artículo contra la BD (por código y por código de barras como fallback)
        await ValidarArticulosAsync(compraExtraida.detalles);

        // 6. Completar datos del contexto (proveedor, usuario, sucursal)
        compraExtraida.idproveedor = idProveedor;
        compraExtraida.idusuario = idUsuario;
        compraExtraida.idSucursal = idSucursal;

        return new AnalizarPdfResponseDto
        {
            compra               = compraExtraida,
            urlsImagenesCloudinary = urlsCloudinary,
            proveedorDetectado   = proveedorDetectado,
            advertencias         = advertencias,
            modeloUsado          = modeloUsado
        };
    }

    // ─── Conversión PDF → imágenes ───────────────────────────────────────────

    private List<byte[]> ConvertirPdfAImagenes(byte[] pdfBytes)
    {
        var imagenes = new List<byte[]>();

        using var stream = new MemoryStream(pdfBytes);
        var paginas = Conversion.ToImages(stream, options: new RenderOptions { Dpi = 300 });

        foreach (var bitmap in paginas)
        {
            using (bitmap)
            {
                using var data = bitmap.Encode(SKEncodedImageFormat.Jpeg, 85);
                imagenes.Add(data.ToArray());
            }
        }

        _logger.LogInformation("PDF convertido a {Paginas} imagen(es).", imagenes.Count);
        return imagenes;
    }

    // ─── Cloudinary upload ───────────────────────────────────────────────────

    private async Task<List<string>> SubirImagenesACloudinaryAsync(List<byte[]> imagenes)
    {
        var urls = new List<string>();
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        for (int i = 0; i < imagenes.Count; i++)
        {
            try
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(
                        $"compra_{timestamp}_p{i + 1}.jpg",
                        new MemoryStream(imagenes[i])),
                    Folder = "VariedadesAby/compras_facturas",
                    PublicId = $"compra_{timestamp}_p{i + 1}",
                    UseFilename = false
                };

                var result = await _cloudinary.UploadAsync(uploadParams);
                urls.Add(result.SecureUrl.ToString());

                _logger.LogInformation(
                    "Factura p.{Pagina} subida a Cloudinary: {Url}", i + 1, result.SecureUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "No se pudo subir la imagen de la página {Pagina} a Cloudinary.", i + 1);
            }
        }

        return urls;
    }

    // ─── Gemini Vision ───────────────────────────────────────────────────────

    private async Task<(CompraExtraidaDto compra, string? proveedor)> ExtraerDatosConGeminiAsync(byte[] pdfBytes, string modelo)
    {
        _logger.LogInformation("Enviando el PDF completo de forma nativa a Gemini ({Modelo})...", modelo);

        const string prompt = """
            Eres un extractor experto de facturas de proveedor. Analiza este PDF y extrae TODOS los productos.

            ESTRUCTURA JSON DE SALIDA:
            {
              "esFactura": true,
              "nombreProveedor": "nombre del proveedor o empresa emisora",
              "numComprobante": "número de factura o comprobante",
              "serieComprobante": "serie si existe, sino null",
              "tipoComprobante": "CONTADO o CREDITO",
              "impuesto": 0.00,
              "total": 0.00,
              "detalles": [
                {
                  "codigo": "código interno / referencia / SKU del producto, sino null",
                  "codigoBarras": "EAN-13 o UPC-A si existe, sino null",
                  "nombreArticulo": "nombre completo del producto",
                  "descripcionArticulo": "descripción adicional si existe, sino null",
                  "unidadMedida": "unidad original de la factura: DOC, PAR, PZA, UND, etc. null si no hay",
                  "cantidad": 8,
                  "marca": "marca si aparece, sino null"
                }
              ]
            }

            ── REGLA 1: LÍNEAS A INCLUIR Y EXCLUIR ──
            INCLUIR solo filas con código o nombre Y cantidad de un producto real.
            EXCLUIR completamente (no generes objeto JSON para estas filas):
            - Subtotales parciales ("Subtotal", "Sub-Total", "Importe Parcial", "Parcial")
            - Filas de impuesto ("IVA", "Tax", "Impuesto", "I.V.A.")
            - Filas de descuento global ("Descuento general", "Discount")
            - Fila del total final ("Total", "Grand Total", "Importe Total", "TOTAL A PAGAR")
            - Encabezados de sección o categoría (filas sin precio ni cantidad numérica)
            - Notas, leyendas, términos y condiciones al pie de página

            ── REGLA 2: CAMPO "codigo" ──
            - Usa el código INTERNO del proveedor: columnas "Referencia", "Ref.", "Código", "SKU", "Clave", "Artículo", "Código Interno".
            - NUNCA uses un EAN-13 (13 dígitos) ni UPC-A (12 dígitos) como "codigo". Ese valor va en "codigoBarras".
            - Si no existe código interno visible, usa null.

            ── REGLA 3: CANTIDAD Y UNIDAD (EXTRACCIÓN EXACTA, SIN CONVERTIR) ──
            Extrae los valores EXACTAMENTE como aparecen en la factura. NO conviertas ni calcules.
            - "cantidad": el número exacto de la columna "Cantidad", "Cant.", "Qty". No multipliques.
            - "unidadMedida": la unidad de la columna "Und", "Unidad", "Unit". Usa null si no aparece.

            CASO ESPECIAL — Sin columna de unidad:
            Si la factura no tiene columna de unidad, infiere basándote en la magnitud:
            - cantidadLeída < 20 → unidadMedida="DOC" (este negocio compra en grandes volúmenes)
            - cantidadLeída ≥ 20 → unidadMedida=null (ya son unidades)

            EJEMPLOS:
            Factura: Cant=8, Und=DOC → devuelves: cantidad=8, unidadMedida="DOC"
            Factura: Cant=10, sin unidad → devuelves: cantidad=10, unidadMedida="DOC"
            Factura: Cant=144, sin unidad → devuelves: cantidad=144, unidadMedida=null

            ── REGLA 5: TIPO DE COMPROBANTE ──
            - "CREDITO" si la factura indica: "Crédito", "A Crédito", "Plazo", "Credit", días de crédito.
            - "CONTADO" en cualquier otro caso.

            ── REGLA 6: TOTALES ──
            - "total": monto total de la factura SIN impuesto. Si el PDF solo muestra total con IVA, resta: total = TotalConIVA - IVA.
            - "impuesto": monto del IVA o impuesto separado. Si no hay, usa 0.

            ── REGLA FINAL ──
            - Si el documento NO es factura ni comprobante de compra: devuelve { "esFactura": false }
            - Por cada fila de producto: genera EXACTAMENTE UN objeto en "detalles". Ni más ni menos.
            - Recorre TODAS las páginas del PDF. No omitas ninguna página.
            """;

        var parts = new List<Part>
        {
            new Part { Text = prompt },
            new Part { InlineData = new Blob { MimeType = "application/pdf", Data = pdfBytes } }
        };

        var config = new GenerateContentConfig
        {
            Temperature    = 0.1f,
            MaxOutputTokens = 65536,
            ResponseMimeType = "application/json"
        };

        var response = await _geminiClient.Models.GenerateContentAsync(
            model: modelo,
            contents: new List<Content> { new Content { Role = "user", Parts = parts } },
            config: config
        );

        var jsonRaw = response.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim()
            ?? throw new InvalidOperationException("Gemini no devolvió respuesta al analizar la factura.");

        _logger.LogInformation("Gemini extrajo datos. JSON crudo (primeros 300 chars): {Json}",
            jsonRaw[..Math.Min(300, jsonRaw.Length)]);

        return DeserializarRespuesta(jsonRaw);
    }

    private (CompraExtraidaDto compra, string? proveedor) DeserializarRespuesta(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        try
        {
            var datos = JsonSerializer.Deserialize<GeminiFacturaDto>(json, options)
                ?? throw new InvalidOperationException("La respuesta de Gemini está vacía.");

            if (!datos.EsFactura)
                throw new InvalidOperationException(
                    "El documento no parece ser una factura de proveedor. " +
                    "Verifica que el PDF corresponda a un comprobante de compra.");

            var compra = new CompraExtraidaDto
            {
                num_comprobante   = datos.NumComprobante ?? string.Empty,
                serie_comprobante = datos.SerieComprobante,
                tipo_comprobante  = datos.TipoComprobante ?? "CONTADO",
                impuesto          = datos.Impuesto ?? 0m,
                total             = datos.Total ?? 0m,
                detalles          = datos.Detalles?.Select(d => new DetalleCompraExtraidoDto
                {
                    codigo              = d.Codigo,
                    codigoBarras        = d.CodigoBarras,
                    nombreArticulo      = d.NombreArticulo,
                    descripcionArticulo = d.DescripcionArticulo,
                    cantidad            = ConvertirAUnidades(d.Cantidad, d.UnidadMedida),
                    precio              = 0,
                    idarticulo          = 0,
                    marca               = d.Marca
                }).ToList() ?? []
            };

            return (compra, datos.NombreProveedor);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Error al deserializar respuesta de Gemini. JSON completo recibido:\n{Json}", json);
            throw new InvalidOperationException(
                "No se pudieron interpretar los datos extraídos del PDF. " +
                "Revisa que el PDF sea legible e inténtalo de nuevo.");
        }
    }

    // ─── Validar artículo individual por código ──────────────────────────────

    public async Task<ValidarArticuloDto> ValidarArticuloAsync(string codigo)
    {
        var articulo = await _dbConnection.QueryFirstOrDefaultAsync<ArticuloDto>(
            @"SELECT idarticulo, nombre, RTRIM(codigo) AS codigo, idcategoria, precio_venta, precio_compra
                FROM dbo.articulo WITH (NOLOCK)
               WHERE RTRIM(codigo) = @codigo",
            new { codigo = codigo.Trim() });

        if (articulo is null)
            return new ValidarArticuloDto { EsNuevo = true };

        return new ValidarArticuloDto
        {
            EsNuevo      = false,
            IdArticulo   = articulo.IdArticulo,
            Nombre       = articulo.Nombre,
            Codigo       = articulo.Codigo,
            IdCategoria  = articulo.IdCategoria,
            PrecioVenta  = articulo.PrecioVenta,
            PrecioCompra = articulo.PrecioCompra
        };
    }

    // ─── Validación de artículos contra la BD ────────────────────────────────

    private async Task ValidarArticulosAsync(List<DetalleCompraExtraidoDto> detalles)
    {
        var codigos = detalles
            .Where(d => !string.IsNullOrWhiteSpace(d.codigo))
            .Select(d => d.codigo!.Trim())
            .Distinct()
            .ToList();

        if (codigos.Count == 0) return;

        // RTRIM previene falsos negativos por espacios almacenados en la BD
        var articulos = (await _dbConnection.QueryAsync<ArticuloDto>(
            @"SELECT a.idarticulo, a.nombre, RTRIM(a.codigo) AS codigo, a.idcategoria, a.precio_venta
                FROM articulo a WITH (NOLOCK)
               WHERE RTRIM(a.codigo) IN @codigos",
            new { codigos }))
            .GroupBy(a => (a.Codigo ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var detalle in detalles)
        {
            if (string.IsNullOrWhiteSpace(detalle.codigo)) continue;

            var codigoLimpio = detalle.codigo.Trim();
            if (articulos.TryGetValue(codigoLimpio, out var encontrado))
            {
                detalle.idarticulo   = encontrado.IdArticulo;
                detalle.idCategoria  = encontrado.IdCategoria;
                detalle.precio_venta = encontrado.PrecioVenta;
                if (string.IsNullOrWhiteSpace(detalle.nombreArticulo))
                    detalle.nombreArticulo = encontrado.Nombre;
            }
            else
            {
                detalle.idarticulo = 0;
            }
        }
    }

    // ─── Conversión de unidades → piezas individuales ───────────────────────

    private static int ConvertirAUnidades(decimal cantidad, string? unidad)
    {
        var u = (unidad ?? string.Empty).Trim().ToUpperInvariant();

        int factor = u switch
        {
            "DOC" or "DOCENA" or "DOCENAS"
                 or "DOZ" or "DZ" or "DOZEN"
                 or "DS" or "DZN"              => 12,
            "PAR" or "PR" or "PARES"           => 2,
            "GRUESA" or "GR" or "GROSS"        => 144,
            "MEDIA DOCENA" or "1/2 DOC"        => 6,
            _                                  => 1
        };

        var result = (int)Math.Round(cantidad * factor);
        return result > 0 ? result : 1;
    }

    // ─── Agrupación y depuración de duplicados ─────────────────────────────

    private static List<DetalleCompraExtraidoDto> AgruparDetalles(List<DetalleCompraExtraidoDto> detalles)
    {
        return detalles
            .GroupBy(d => new 
            { 
                Codigo = d.codigo?.Trim() ?? string.Empty, 
                Precio = Math.Round(d.precio, 2) 
            })
            .Select(g => 
            {
                var principal = g.First();
                principal.cantidad = g.Sum(x => x.cantidad);
                return principal;
            })
            .ToList();
    }

    // ─── Validación de coherencia numérica ──────────────────────────────────

    private List<string> ValidarCoherenciaTotales(CompraExtraidaDto compra)
    {
        var advertencias = new List<string>();

        if (compra.total <= 0 || compra.detalles.Count == 0)
            return advertencias;

        var sumaDetalles = compra.detalles.Sum(d => d.cantidad * d.precio);
        var diferencia   = Math.Abs(sumaDetalles - compra.total);
        var porcentaje   = diferencia / compra.total;

        if (porcentaje > 0.05m)
        {
            var msg = $"La suma de los artículos ({sumaDetalles:N2}) difiere del total de la factura ({compra.total:N2}) en un {porcentaje:P0}. Revisa los precios y cantidades antes de confirmar.";
            advertencias.Add(msg);
            _logger.LogWarning("Discrepancia en totales PDF: {Msg}", msg);
        }

        return advertencias;
    }

    // ─── Reintentos con degradación gradual de modelos (calidad primero) ────

    private async Task<(T valor, string modeloUsado)> EjecutarConReintentoAsync<T>(Func<string, Task<T>> funcion)
    {
        // PDF requiere máxima precisión → priorizar modelo premium con más intentos.
        // Cadena de degradación gradual:
        //   gemini-2.5-pro  (4 intentos) → mejor calidad, puede estar saturado
        //   gemini-2.5-flash (3 intentos) → buena calidad, alta disponibilidad
        //   gemini-2.0-flash (2 intentos) → último recurso, calidad aceptable
        var cadenaModelos = new (string modelo, int maxIntentos)[]
        {
            (_modelo, 4),
            ("gemini-2.5-flash", 3),
            ("gemini-2.0-flash", 2)
        };

        // Deduplicar por si _modelo ya es uno de los fallback
        var modelosUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Exception? ultimaExcepcion = null;

        foreach (var (modelo, maxIntentos) in cadenaModelos)
        {
            if (!modelosUsados.Add(modelo)) continue; // ya se intentó

            var resultado = await IntentarConModeloAsync(funcion, modelo, maxIntentos);
            if (resultado.Exito)
            {
                if (modelo != _modelo)
                    _logger.LogWarning("[PDF] Extracción con modelo de respaldo '{Modelo}'. La precisión podría variar.", modelo);
                else
                    _logger.LogInformation("[PDF] Extracción exitosa con modelo principal '{Modelo}'.", modelo);
                return (resultado.Valor!, modelo);
            }

            ultimaExcepcion = resultado.Excepcion;
            _logger.LogWarning(
                "[PDF] Modelo '{Modelo}' falló tras {Max} intentos. Probando siguiente...",
                modelo, maxIntentos);
        }

        throw ultimaExcepcion ?? new InvalidOperationException("Todos los modelos de Gemini fallaron al procesar el PDF.");
    }

    private async Task<(bool Exito, T? Valor, Exception? Excepcion)> IntentarConModeloAsync<T>(
        Func<string, Task<T>> funcion, string modelo, int maxIntentos)
    {
        int delay = INITIAL_DELAY_MS;

        for (int i = 1; i <= maxIntentos; i++)
        {
            try
            {
                return (true, await funcion(modelo), null);
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                if (i == maxIntentos)
                    return (false, default, ex);

                _logger.LogWarning(
                    "[PDF] '{Modelo}' sobrecargado. Reintentando en {Delay}ms ({I}/{Max}). Error: {Msg}",
                    modelo, delay, i, maxIntentos, ex.Message);
                await Task.Delay(delay);
                delay *= 2;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[PDF] Error no reintentable en '{Modelo}': {Msg}", modelo, ex.Message);
                return (false, default, ex);
            }
        }

        return (false, default, new InvalidOperationException("Código inalcanzable"));
    }

    private static bool IsRetryable(Exception ex) =>
        ex.Message.Contains("Resource exhausted", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("429")
        || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("Quota exceeded", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("high demand", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("overloaded", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("503");

    // ─── DTOs internos ───────────────────────────────────────────────────────

    private sealed class GeminiFacturaDto
    {
        public bool EsFactura { get; set; } = true;
        public string? NombreProveedor { get; set; }
        public string? NumComprobante { get; set; }
        public string? SerieComprobante { get; set; }
        public string? TipoComprobante { get; set; }
        public decimal? Impuesto { get; set; }
        public decimal? Total { get; set; }
        public List<GeminiDetalleDto>? Detalles { get; set; }
    }

    private sealed class GeminiDetalleDto
    {
        public string? Codigo { get; set; }
        public string? CodigoBarras { get; set; }
        public string? NombreArticulo { get; set; }
        public string? DescripcionArticulo { get; set; }
        public string? UnidadMedida { get; set; }
        public decimal Cantidad { get; set; }
        public string? Marca { get; set; }
    }

    private sealed class ArticuloDto
    {
        public int IdArticulo { get; set; }
        public string? Nombre { get; set; }
        public string? Codigo { get; set; }
        public int? IdCategoria { get; set; }
        public string? NombreCategoria { get; set; }
        public decimal? PrecioVenta { get; set; }
        public decimal? PrecioCompra { get; set; }
    }
}
