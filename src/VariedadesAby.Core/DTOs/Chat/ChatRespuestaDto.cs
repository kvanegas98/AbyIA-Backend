namespace VariedadesAby.Core.DTOs.Chat;

/// <summary>
/// Respuesta completa del módulo Aby IA
/// </summary>
public record ChatRespuestaDto
{
    public string Pregunta { get; init; } = string.Empty;
    public string SqlGenerado { get; init; } = string.Empty;
    public string Respuesta { get; init; } = string.Empty;
    public object? Datos { get; init; }
    public bool Exito { get; init; }
    public DateTime Fecha { get; init; }
    public string? ModeloUsado { get; init; }
}
