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
    public async Task GuardarAsync(int idUsuario, string pregunta, string? sqlGenerado, string? respuestaIA, bool exito)
    {
        const string sql = @"
            INSERT INTO dbo.HistorialChat (IdUsuario, Pregunta, SqlGenerado, RespuestaIA, Exito)
            VALUES (@IdUsuario, @Pregunta, @SqlGenerado, @RespuestaIA, @Exito);";

        await _connection.ExecuteAsync(sql, new
        {
            IdUsuario   = idUsuario,
            Pregunta    = pregunta,
            SqlGenerado = sqlGenerado,
            RespuestaIA = respuestaIA,
            Exito       = exito
        });
    }

    // ── sp_HistorialChat_Obtener ─────────────────────────────────────────────
    public async Task<IEnumerable<HistorialChatDto>> ObtenerHistorialAsync(int idUsuario, int cantidad = 20)
    {
        const string sql = @"
            SELECT TOP (@Cantidad)
                Id,
                Pregunta,
                RespuestaIA,
                Fecha,
                Exito
            FROM dbo.HistorialChat WITH (NOLOCK)
            WHERE IdUsuario = @IdUsuario
            ORDER BY Fecha DESC;";

        return await _connection.QueryAsync<HistorialChatDto>(sql, new
        {
            IdUsuario = idUsuario,
            Cantidad  = cantidad
        });
    }
}
