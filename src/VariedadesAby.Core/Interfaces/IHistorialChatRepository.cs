using VariedadesAby.Core.DTOs.Chat;

namespace VariedadesAby.Core.Interfaces;

/// <summary>
/// Repositorio para el historial de conversaciones del módulo Aby IA
/// </summary>
public interface IHistorialChatRepository
{
    Task GuardarAsync(int idUsuario, string pregunta, string? sqlGenerado, string? respuestaIA, bool exito);
    Task<IEnumerable<HistorialChatDto>> ObtenerHistorialAsync(int idUsuario, int cantidad = 20);
}
