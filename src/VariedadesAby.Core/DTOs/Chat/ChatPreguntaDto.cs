namespace VariedadesAby.Core.DTOs.Chat;

/// <summary>
/// Pregunta del usuario para el módulo Aby IA (Text-to-SQL)
/// </summary>
public record ChatPreguntaDto
{
    public string Pregunta { get; init; } = string.Empty;
}
