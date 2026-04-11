using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapper;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VariedadesAby.Core.DTOs.Chat;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class AbyIAService : IAbyIAService
{
    private readonly IDbConnection _aiConnection;
    private readonly IHistorialChatRepository _historialRepo;
    private readonly Client _geminiClient;
    private readonly string _modelo;
    private readonly string _modeloFallback;
    private readonly string _esquemaBD;
    private readonly ILogger<AbyIAService> _logger;

    private string? _ultimoModeloUsado;

    private const int MAX_RETRIES    = 3;
    private const int INITIAL_DELAY_MS = 2000;

    private static readonly string[] _palabrasProhibidas =
    {
        "INSERT", "UPDATE", "DELETE", "DROP", "ALTER", "TRUNCATE",
        "EXEC", "EXECUTE", "CREATE", "GRANT", "REVOKE", "DENY",
        "MERGE", "BULK", "OPENROWSET", "OPENDATASOURCE", "xp_", "sp_"
    };

    public AbyIAService(
        [FromKeyedServices("AI_ReadOnly")] IDbConnection aiConnection,
        IHistorialChatRepository historialRepo,
        IConfiguration configuration,
        ILogger<AbyIAService> logger)
    {
        _aiConnection = aiConnection;
        _historialRepo = historialRepo;
        _logger = logger;

        var apiKey = configuration["GeminiAI:Key"]
            ?? configuration["GeminiAI:ApiKey"]
            ?? throw new InvalidOperationException("GeminiAI:Key no está configurada en appsettings.json");

        _modelo         = configuration["GeminiAI:Modelo"]         ?? "gemini-2.5-pro";
        _modeloFallback = configuration["GeminiAI:ModeloFallback"]  ?? "gemini-2.0-flash";
        _geminiClient   = new Client(apiKey: apiKey);

        _esquemaBD = configuration["GeminiAI:EsquemaBD"]
            ?? "No se proporcionó esquema de base de datos.";
    }

    // ── Punto de entrada principal ───────────────────────────────────────────
    public async Task<ChatRespuestaDto> ProcesarPreguntaAsync(string pregunta, int idUsuario)
    {
        string? sqlGenerado      = null;
        string? respuestaAmigable = null;
        bool exito               = false;
        _ultimoModeloUsado       = null;

        try
        {
            var historial        = await _historialRepo.ObtenerHistorialAsync(idUsuario, 6);
            var historialContent = ConstruirHistorialContent(historial);

            // ── Un solo llamado a Gemini devuelve JSON estructurado ──────────
            var intencion = await AnalizarPreguntaAsync(pregunta, historialContent);

            switch (intencion.Tipo.ToUpperInvariant())
            {
                case "SQL":
                    sqlGenerado = intencion.Contenido;
                    ValidarSqlSeguro(sqlGenerado);
                    var datos = await EjecutarSqlAsync(sqlGenerado);
                    respuestaAmigable = await InterpretarResultadosAsync(pregunta, sqlGenerado, datos);
                    exito = true;
                    return new ChatRespuestaDto
                    {
                        Pregunta    = pregunta,
                        SqlGenerado = sqlGenerado,
                        Respuesta   = respuestaAmigable,
                        Datos       = datos,
                        Exito       = true,
                        Fecha       = DateTime.Now,
                        ModeloUsado = _ultimoModeloUsado
                    };

                default: // ACLARAR | NO_APLICA | CONVERSACIONAL
                    sqlGenerado       = $"N/A - {intencion.Tipo}";
                    respuestaAmigable = intencion.Contenido;
                    exito             = true;
                    break;
            }

            return new ChatRespuestaDto
            {
                Pregunta    = pregunta,
                SqlGenerado = sqlGenerado,
                Respuesta   = respuestaAmigable,
                Datos       = null,
                Exito       = true,
                Fecha       = DateTime.Now,
                ModeloUsado = _ultimoModeloUsado
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error procesando pregunta de IA: {Pregunta}", pregunta);

            respuestaAmigable = IsResourceExhausted(ex) || IsEmptyResponse(ex)
                ? "El servicio de IA está temporalmente saturado. Intenta nuevamente en unos segundos."
                : ex switch
                {
                    InvalidOperationException => ex.Message,
                    _ => $"No pude procesar tu pregunta. Error: {ex.Message}"
                };

            return new ChatRespuestaDto
            {
                Pregunta    = pregunta,
                SqlGenerado = sqlGenerado ?? string.Empty,
                Respuesta   = respuestaAmigable,
                Datos       = null,
                Exito       = false,
                Fecha       = DateTime.Now,
                ModeloUsado = _ultimoModeloUsado
            };
        }
        finally
        {
            try
            {
                await _historialRepo.GuardarAsync(idUsuario, pregunta, sqlGenerado, respuestaAmigable, exito);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error guardando historial de chat");
            }
        }
    }

    // ── Structured Output: Gemini devuelve JSON garantizado ──────────────────
    private async Task<GeminiIntencion> AnalizarPreguntaAsync(string pregunta, List<Content> historialContent)
    {
        var systemPrompt = $@"Eres un asistente de datos para el sistema Variedades Aby (Nicaragua).
Analiza la pregunta del usuario y devuelve una respuesta estructurada.

TIPOS DE RESPUESTA:
- SQL           → La pregunta requiere consultar datos. Devuelve el SELECT completo en 'contenido'.
- ACLARAR       → La pregunta es ambigua y falta un parámetro clave (período, cliente, sucursal). Devuelve la pregunta de aclaración en 'contenido'.
- NO_APLICA     → Solicita modificar datos o está fuera del sistema. Devuelve explicación breve.
- CONVERSACIONAL→ Saludo, pregunta sobre el asistente, etc. Responde amigablemente.

REGLAS SQL (solo para tipo SQL):
1. Solo SELECT. Nunca INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, EXEC ni escrituras.
2. Usa exactamente los nombres de tabla y columna del esquema.
3. WITH (NOLOCK) siempre: alias ANTES del WITH → `FROM dbo.venta v WITH (NOLOCK)`.
4. TOP 100 por defecto salvo que el usuario pida cantidad específica.
5. Excluye anulados: `estado != 'Anulado'` en venta, ingreso y NotaCredito.
6. Stock de artículos: usar `sucursalArticulo.stock`. No existe columna stock en articulo.
7. Ventas a crédito: `tipo_comprobante = 'CREDITO'`.
8. Fechas: `fecha_hora >= 'YYYY-MM-DD' AND fecha_hora < 'YYYY-MM-DD'`.
9. Columnas de dinero: `CONCAT('C$ ', FORMAT(columna, 'N2'))`.
10. No incluyas IDs numéricos en el SELECT final; usa alias descriptivos en español.
11. HAVING vs WHERE: usa HAVING SOLO cuando la query externa tiene GROUP BY y filtras sobre funciones de agregación (SUM, COUNT, etc.). Para filtrar columnas de subqueries o JOINs sin GROUP BY externo, usa siempre WHERE.

CONTEXTO DE CONVERSACIÓN:
- Si el mensaje actual es solo una fecha/período ('diciembre 2025', 'este mes', 'últimos 30 días')
  y el historial tiene una pregunta pendiente, combínalos y genera el SQL completo.
- Si la pregunta usa 'ese cliente', 'ese artículo', etc., busca el contexto en el historial.

CÁLCULO DE DEUDA (usa esta plantilla exacta cuando el usuario pregunte por deudas o saldos):
SELECT TOP 100
    p.nombre AS [Cliente],
    CONCAT('C$ ', FORMAT(ISNULL(v_tot.TotalAPagar,   0), 'N2')) AS [Total Comprado],
    CONCAT('C$ ', FORMAT(ISNULL(ab_tot.TotalAbonos,  0), 'N2')) AS [Total Abonado],
    CONCAT('C$ ', FORMAT(ISNULL(nc_tot.TotalNC,      0), 'N2')) AS [Notas de Crédito],
    CONCAT('C$ ', FORMAT(ISNULL(v_tot.TotalAPagar,0) - ISNULL(ab_tot.TotalAbonos,0) - ISNULL(nc_tot.TotalNC,0), 'N2')) AS [Saldo Pendiente]
FROM dbo.credito c WITH (NOLOCK)
INNER JOIN dbo.persona p WITH (NOLOCK) ON p.idpersona = c.Id_Persona
LEFT JOIN (SELECT v.IdCredito, SUM(v.total) AS TotalAPagar FROM dbo.venta v WITH (NOLOCK) WHERE v.estado != 'Anulado' GROUP BY v.IdCredito) v_tot ON v_tot.IdCredito = c.Id_Credito
LEFT JOIN (SELECT ab.Id_Credito, SUM(ab.Monto) AS TotalAbonos FROM dbo.abono ab WITH (NOLOCK) WHERE ab.Id_Estado = 0 GROUP BY ab.Id_Credito) ab_tot ON ab_tot.Id_Credito = c.Id_Credito
LEFT JOIN (SELECT v.IdCredito, SUM(nc.Total) AS TotalNC FROM dbo.NotaCredito nc WITH (NOLOCK) INNER JOIN dbo.venta v WITH (NOLOCK) ON v.idventa = nc.IdVenta WHERE nc.estado != 'Anulado' GROUP BY v.IdCredito) nc_tot ON nc_tot.IdCredito = c.Id_Credito
LEFT JOIN (SELECT Id_Credito, MAX(FechaDePago) AS UltimoAbono FROM dbo.abono WITH (NOLOCK) WHERE Id_Estado = 0 GROUP BY Id_Credito) last_ab ON last_ab.Id_Credito = c.Id_Credito
WHERE c.Id_Estado = 1
-- Filtros con WHERE (nunca HAVING aquí, no hay GROUP BY externo):
-- Saldo pendiente > 0        → AND (ISNULL(v_tot.TotalAPagar,0) - ISNULL(ab_tot.TotalAbonos,0) - ISNULL(nc_tot.TotalNC,0)) > 0
-- Sin pago en 30+ días       → AND (last_ab.UltimoAbono < DATEADD(day,-30,GETDATE()) OR (last_ab.UltimoAbono IS NULL AND c.PrimerCredito < DATEADD(day,-30,GETDATE())))
-- Cliente específico         → AND p.nombre LIKE '%X%'
-- Mayor deuda primero        → ORDER BY (ISNULL(v_tot.TotalAPagar,0) - ISNULL(ab_tot.TotalAbonos,0) - ISNULL(nc_tot.TotalNC,0)) DESC

ESQUEMA DE BASE DE DATOS:
{_esquemaBD}";

        var contents = new List<Content>();
        contents.AddRange(historialContent);
        contents.Add(new Content
        {
            Role  = "user",
            Parts = new List<Part> { new Part { Text = pregunta } }
        });

        // ResponseSchema garantiza que Gemini devuelva JSON válido con los campos correctos
        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = new List<Part> { new Part { Text = systemPrompt } }
            },
            Temperature      = 0.1f,
            MaxOutputTokens  = 3072,
            ResponseMimeType = "application/json",
            ResponseSchema   = new Schema
            {
                Type       = Google.GenAI.Types.Type.Object,
                Properties = new Dictionary<string, Schema>
                {
                    ["tipo"]     = new Schema
                    {
                        Type = Google.GenAI.Types.Type.String,
                        Enum = new List<string> { "SQL", "ACLARAR", "NO_APLICA", "CONVERSACIONAL" }
                    },
                    ["contenido"] = new Schema
                    {
                        Type = Google.GenAI.Types.Type.String
                    }
                },
                Required = new List<string> { "tipo", "contenido" }
            }
        };

        var json = await EjecutarConReintentoAsync(async (modelo) =>
        {
            var response = await _geminiClient.Models.GenerateContentAsync(
                model: modelo,
                contents: contents,
                config: config
            );

            var texto = response.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(texto))
            {
                var reason = response.Candidates?[0]?.FinishReason;
                _logger.LogWarning("[IA] Respuesta vacía en '{Modelo}'. FinishReason: {Reason}", modelo, reason);
                throw new InvalidOperationException($"EMPTY_RESPONSE|Gemini devolvió respuesta vacía (FinishReason: {reason})");
            }

            return texto;
        }, "Analizar Pregunta");

        var intencion = JsonSerializer.Deserialize<GeminiIntencion>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (intencion is null || string.IsNullOrWhiteSpace(intencion.Tipo))
            throw new InvalidOperationException("La respuesta de Gemini no tiene el formato esperado.");

        return intencion;
    }

    // ── Historial de conversación ────────────────────────────────────────────
    private static List<Content> ConstruirHistorialContent(IEnumerable<HistorialChatDto> historial)
    {
        var contentList = new List<Content>();

        foreach (var item in historial.Where(h => h.Exito).Reverse())
        {
            contentList.Add(new Content
            {
                Role  = "user",
                Parts = new List<Part> { new Part { Text = item.Pregunta } }
            });

            contentList.Add(new Content
            {
                Role  = "model",
                Parts = new List<Part>
                {
                    new Part { Text = !string.IsNullOrWhiteSpace(item.RespuestaIA)
                        ? item.RespuestaIA
                        : "Entendido." }
                }
            });
        }

        return contentList;
    }

    // ── Seguridad: bloquea cualquier SQL que no sea SELECT ───────────────────
    private static void ValidarSqlSeguro(string sql)
    {
        var sqlUpper = sql.ToUpperInvariant();

        if (!sqlUpper.TrimStart().StartsWith("SELECT") && !sqlUpper.TrimStart().StartsWith("WITH"))
            throw new InvalidOperationException(
                "La consulta generada no es una consulta de lectura válida.");

        foreach (var palabra in _palabrasProhibidas)
        {
            if (Regex.IsMatch(sqlUpper, $@"\b{Regex.Escape(palabra)}\b", RegexOptions.IgnoreCase))
                throw new InvalidOperationException(
                    $"La consulta contiene una operación no permitida: {palabra}.");
        }
    }

    // ── Ejecuta el SQL en la conexión de solo lectura ────────────────────────
    private async Task<object?> EjecutarSqlAsync(string sql)
    {
        var resultado = await _aiConnection.QueryAsync(sql, commandTimeout: 30);
        return resultado.Select(row => (IDictionary<string, object>)row).ToList();
    }

    // ── Interpreta los resultados en lenguaje natural ────────────────────────
    private async Task<string> InterpretarResultadosAsync(string pregunta, string sql, object? datos)
    {
        var datosJson = JsonSerializer.Serialize(datos, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (datosJson.Length > 8000)
            datosJson = datosJson[..8000] + "... (datos truncados)";

        var systemPrompt = @"Eres Chat Aby IA, asistente del sistema Variedades Aby.
Interpreta resultados de consultas SQL y responde al usuario de forma amigable en español.
- Usa texto simple, sin markdown complejo.
- Presenta moneda como 'C$ X,XXX.XX'.
- Si hay muchos datos, resume los puntos más importantes.
- Si no hay datos, indícalo amablemente.
- Nunca muestres el SQL al usuario.";

        var userPrompt = $@"El usuario preguntó: ""{pregunta}""
Resultados ({(datos is IList<object> list ? list.Count : 0)} filas):
{datosJson}
Interpreta estos resultados de forma clara y amigable.";

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = new List<Part> { new Part { Text = systemPrompt } } },
            Temperature       = 0.7f,
            MaxOutputTokens   = 2048
        };

        return await EjecutarConReintentoAsync(async (modelo) =>
        {
            var response = await _geminiClient.Models.GenerateContentAsync(
                model: modelo, contents: userPrompt, config: config);

            var texto = response.Candidates?[0]?.Content?.Parts?[0]?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(texto))
            {
                var reason = response.Candidates?[0]?.FinishReason;
                throw new InvalidOperationException($"EMPTY_RESPONSE|Respuesta vacía (FinishReason: {reason})");
            }

            return texto;
        }, "Interpretar Resultados");
    }

    // ── Retry con fallback de modelos ────────────────────────────────────────
    private async Task<T> EjecutarConReintentoAsync<T>(Func<string, Task<T>> funcion, string operacion)
    {
        var r1 = await IntentarConModeloAsync(funcion, _modelo, operacion);
        if (r1.Exito) { _ultimoModeloUsado = _modelo; return r1.Valor!; }

        _logger.LogWarning("[IA] '{Modelo}' falló en '{Op}'. Cambiando a '{Fallback}'.",
            _modelo, operacion, _modeloFallback);

        var r2 = await IntentarConModeloAsync(funcion, _modeloFallback, operacion);
        if (r2.Exito) { _ultimoModeloUsado = _modeloFallback; return r2.Valor!; }

        const string flash = "gemini-2.0-flash";
        _logger.LogWarning("[IA] '{Fallback}' también falló en '{Op}'. Último recurso: '{Flash}'.",
            _modeloFallback, operacion, flash);

        var r3 = await IntentarConModeloAsync(funcion, flash, operacion);
        if (r3.Exito) { _ultimoModeloUsado = flash; return r3.Valor!; }

        throw r3.UltimaExcepcion!;
    }

    private async Task<(bool Exito, T? Valor, Exception? UltimaExcepcion)> IntentarConModeloAsync<T>(
        Func<string, Task<T>> funcion, string modelo, string operacion)
    {
        int delay = INITIAL_DELAY_MS;

        for (int i = 1; i <= MAX_RETRIES; i++)
        {
            try
            {
                return (true, await funcion(modelo), null);
            }
            catch (Exception ex) when (IsRetryable(ex))
            {
                if (i == MAX_RETRIES)
                {
                    _logger.LogWarning("[IA] '{Modelo}' agotó {Max} intentos en '{Op}'. Error: {Msg}",
                        modelo, MAX_RETRIES, operacion, ex.Message);
                    return (false, default, ex);
                }

                _logger.LogWarning("[IA] Reintentando '{Modelo}' / '{Op}' en {Delay}ms ({I}/{Max}). Error: {Msg}",
                    modelo, operacion, delay, i, MAX_RETRIES, ex.Message);

                await Task.Delay(delay);
                delay *= 2;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[IA] Error fatal en '{Modelo}' / '{Op}': {Msg}", modelo, operacion, ex.Message);
                return (false, default, ex);
            }
        }

        return (false, default, new InvalidOperationException("Código inalcanzable"));
    }

    private static bool IsRetryable(Exception ex)        => IsResourceExhausted(ex) || IsEmptyResponse(ex);
    private static bool IsResourceExhausted(Exception ex) => ex.Message.Contains("Resource exhausted", StringComparison.OrdinalIgnoreCase)
                                                           || ex.Message.Contains("429")
                                                           || ex.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase)
                                                           || ex.Message.Contains("Quota exceeded",    StringComparison.OrdinalIgnoreCase)
                                                           || ex.Message.Contains("high demand",       StringComparison.OrdinalIgnoreCase)
                                                           || ex.Message.Contains("overloaded",        StringComparison.OrdinalIgnoreCase)
                                                           || ex.Message.Contains("503",               StringComparison.OrdinalIgnoreCase);
    private static bool IsEmptyResponse(Exception ex)     => ex.Message.Contains("EMPTY_RESPONSE", StringComparison.Ordinal);

    // ── DTO interno para la respuesta estructurada de Gemini ─────────────────
    private sealed class GeminiIntencion
    {
        [JsonPropertyName("tipo")]
        public string Tipo     { get; init; } = string.Empty;

        [JsonPropertyName("contenido")]
        public string Contenido { get; init; } = string.Empty;
    }
}
