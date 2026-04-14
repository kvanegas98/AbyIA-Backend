namespace VariedadesAby.Core.DTOs.Chat;

/// <summary>
/// Historial de una conversación del módulo Aby IA
/// </summary>
public record HistorialChatDto
{
    public int Id { get; init; }
    public string Pregunta { get; init; } = string.Empty;
    public string? RespuestaIA { get; init; }
    public DateTime Fecha { get; init; }
    public bool Exito { get; init; }
    public string? SqlGenerado { get; init; }
    public string? ModeloUsado { get; init; }
    public string? NombreUsuario { get; init; }
}
