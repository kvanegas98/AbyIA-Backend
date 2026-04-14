using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Chat;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Repositories;

public class HistorialChatRepository : IHistorialChatRepository
{
    private readonly IDbConnection _connection;

    public HistorialChatRepository(IDbConnection connection)
    {
        _connection = connection;
    }

    // ── sp_HistorialChat_Guardar ─────────────────────────────────────────────
    public async Task GuardarAsync(int idUsuario, string pregunta, string? sqlGenerado, string? respuestaIA, bool exito, string? modeloUsado = null)
    {
        const string sql = @"
            INSERT INTO dbo.HistorialChat (IdUsuario, Pregunta, SqlGenerado, RespuestaIA, Exito, ModeloUsado)
            VALUES (@IdUsuario, @Pregunta, @SqlGenerado, @RespuestaIA, @Exito, @ModeloUsado);";

        await _connection.ExecuteAsync(sql, new
        {
            IdUsuario   = idUsuario,
            Pregunta    = pregunta,
            SqlGenerado = sqlGenerado,
            RespuestaIA = respuestaIA,
            Exito       = exito,
            ModeloUsado = modeloUsado
        });
    }

    // ── sp_HistorialChat_Obtener ─────────────────────────────────────────────
    public async Task<IEnumerable<HistorialChatDto>> ObtenerHistorialAsync(int idUsuario, int cantidad = 20)
    {
        var sql = $@"
            SELECT TOP ({cantidad})
                h.Id,
                h.Pregunta,
                h.RespuestaIA,
                h.Fecha,
                h.Exito,
                h.ModeloUsado,
                u.nombre AS NombreUsuario
            FROM dbo.HistorialChat h WITH (NOLOCK)
            INNER JOIN dbo.usuario u WITH (NOLOCK) ON u.idusuario = h.IdUsuario
            WHERE h.IdUsuario = @IdUsuario
            ORDER BY h.Fecha DESC;";

        return await _connection.QueryAsync<HistorialChatDto>(sql, new { IdUsuario = idUsuario });
    }

    // ── Admin: todo el historial (solo idUsuario = 1) ────────────────────────
    public async Task<IEnumerable<HistorialChatDto>> ObtenerTodoHistorialAsync(int cantidad = 50)
    {
        var sql = $@"
            SELECT TOP ({cantidad})
                h.Id,
                h.Pregunta,
                h.RespuestaIA,
                h.SqlGenerado,
                h.Fecha,
                h.Exito,
                h.ModeloUsado,
                u.nombre AS NombreUsuario
            FROM dbo.HistorialChat h WITH (NOLOCK)
            INNER JOIN dbo.usuario u WITH (NOLOCK) ON u.idusuario = h.IdUsuario
            ORDER BY h.Fecha DESC;";

        return await _connection.QueryAsync<HistorialChatDto>(sql);
    }
}
