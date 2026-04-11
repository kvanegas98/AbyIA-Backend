using VariedadesAby.Core.DTOs.Chat;

namespace VariedadesAby.Core.Interfaces;

/// <summary>
/// Servicio principal del módulo Aby IA (Text-to-SQL)
/// </summary>
public interface IAbyIAService
{
    Task<ChatRespuestaDto> ProcesarPreguntaAsync(string pregunta, int idUsuario);
}
