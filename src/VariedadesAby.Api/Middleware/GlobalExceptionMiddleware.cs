using System.Net;
using System.Text.Json;
using VariedadesAby.Core.Exceptions;
using VariedadesAby.Core.Wrappers;

namespace VariedadesAby.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next   = next;
        _logger = logger;
        _env    = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        // Mapeo de excepciones personalizadas → HTTP status code
        var (statusCode, mensaje) = exception switch
        {
            NotFoundException ex    => (HttpStatusCode.NotFound,            ex.Message),
            IngresoException  ex    => (HttpStatusCode.BadRequest,          ex.Message),
            UnauthorizedAccessException ex => (HttpStatusCode.Unauthorized, ex.Message),
            _                       => (HttpStatusCode.InternalServerError,
                                        _env.IsProduction()
                                            ? "Error interno del servidor."
                                            : exception.Message)
        };

        context.Response.StatusCode = (int)statusCode;

        var response = ApiResponse<object>.Fail(
            mensaje,
            statusCode == HttpStatusCode.InternalServerError && !_env.IsProduction()
                ? new List<string> { exception.StackTrace ?? string.Empty }
                : new List<string>()
        );

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
