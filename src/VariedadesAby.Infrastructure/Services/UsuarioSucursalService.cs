using System.Data;
using Dapper;
using VariedadesAby.Core.DTOs.Usuarios;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class UsuarioSucursalService : IUsuarioSucursalService
{
    private readonly IDbConnection _db;

    public UsuarioSucursalService(IDbConnection db)
    {
        _db = db;
    }

    public async Task<IEnumerable<UsuarioSucursalDto>> ListarAsync()
    {
        const string sql = """
            SELECT
                u.idusuario        AS IdUsuario,
                u.nombre           AS NombreUsuario,
                u.email            AS Email,
                r.nombre           AS Rol,
                u.condicion        AS Activo,
                u.idsucursal       AS IdSucursal,
                s.nombre           AS NombreSucursal,
                us.fechaAsignacion AS FechaAsignacion
            FROM dbo.usuario u WITH (NOLOCK)
            INNER JOIN dbo.rol r WITH (NOLOCK) ON r.idrol = u.idrol
            LEFT JOIN dbo.sucursal s WITH (NOLOCK) ON s.idsucursal = u.idsucursal
            LEFT JOIN dbo.usuariosucursal us WITH (NOLOCK)
                ON us.idusuario = u.idusuario AND us.idsucursal = u.idsucursal
            ORDER BY u.condicion DESC, u.nombre
            """;

        return await _db.QueryAsync<UsuarioSucursalDto>(sql);
    }

    public async Task<IEnumerable<SucursalSimpleDto>> ListarSucursalesAsync()
    {
        const string sql = """
            SELECT idsucursal AS IdSucursal, nombre AS Nombre
            FROM dbo.sucursal WITH (NOLOCK)
            ORDER BY nombre
            """;

        return await _db.QueryAsync<SucursalSimpleDto>(sql);
    }

    public async Task<IEnumerable<SucursalAsignadaDto>> ListarSucursalesUsuarioAsync(int idUsuario)
    {
        const string sql = """
            SELECT
                us.idsucursal      AS IdSucursal,
                s.nombre           AS Nombre,
                us.fechaAsignacion AS FechaAsignacion,
                CAST(CASE WHEN u.idsucursal = us.idsucursal THEN 1 ELSE 0 END AS BIT) AS EsDefault
            FROM dbo.usuariosucursal us WITH (NOLOCK)
            INNER JOIN dbo.sucursal s  WITH (NOLOCK) ON s.idsucursal = us.idsucursal
            INNER JOIN dbo.usuario  u  WITH (NOLOCK) ON u.idusuario  = us.idusuario
            WHERE us.idusuario = @idUsuario
            ORDER BY EsDefault DESC, us.fechaAsignacion DESC
            """;

        return await _db.QueryAsync<SucursalAsignadaDto>(sql, new { idUsuario });
    }

    public async Task AsignarAsync(int idUsuario, int idSucursal)
    {
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var tx = _db.BeginTransaction();
        try
        {
            // Verificar que el usuario existe
            var existe = await _db.ExecuteScalarAsync<bool>(
                "SELECT CAST(COUNT(1) AS BIT) FROM dbo.usuario WITH (NOLOCK) WHERE idusuario = @idUsuario",
                new { idUsuario }, tx);

            if (!existe)
                throw new KeyNotFoundException($"El usuario {idUsuario} no existe.");

            // Verificar que la sucursal existe
            var sucursalExiste = await _db.ExecuteScalarAsync<bool>(
                "SELECT CAST(COUNT(1) AS BIT) FROM dbo.sucursal WITH (NOLOCK) WHERE idsucursal = @idSucursal",
                new { idSucursal }, tx);

            if (!sucursalExiste)
                throw new KeyNotFoundException($"La sucursal {idSucursal} no existe.");

            // 1. Actualizar sucursal por defecto en usuario
            await _db.ExecuteAsync(
                "UPDATE dbo.usuario SET idsucursal = @idSucursal WHERE idusuario = @idUsuario",
                new { idSucursal, idUsuario }, tx);

            // 2. Upsert en usuariosucursal (por combinación usuario+sucursal, no solo usuario)
            const string upsert = """
                IF EXISTS (SELECT 1 FROM dbo.usuariosucursal WHERE idusuario = @idUsuario AND idsucursal = @idSucursal)
                    UPDATE dbo.usuariosucursal
                    SET fechaAsignacion = GETDATE()
                    WHERE idusuario = @idUsuario AND idsucursal = @idSucursal
                ELSE
                    INSERT INTO dbo.usuariosucursal (idusuario, idsucursal, fechaAsignacion)
                    VALUES (@idUsuario, @idSucursal, GETDATE())
                """;

            await _db.ExecuteAsync(upsert, new { idUsuario, idSucursal }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public async Task ReasignarAsync(int idUsuario, int idSucursalVieja, int idSucursalNueva)
    {
        if (_db.State != ConnectionState.Open)
            _db.Open();

        using var tx = _db.BeginTransaction();
        try
        {
            // Quitar la sucursal vieja del historial
            await _db.ExecuteAsync(
                "DELETE FROM dbo.usuariosucursal WHERE idusuario = @idUsuario AND idsucursal = @idSucursalVieja",
                new { idUsuario, idSucursalVieja }, tx);

            // Insertar la nueva (o actualizar fecha si ya existe)
            const string upsert = """
                IF EXISTS (SELECT 1 FROM dbo.usuariosucursal WHERE idusuario = @idUsuario AND idsucursal = @idSucursalNueva)
                    UPDATE dbo.usuariosucursal SET fechaAsignacion = GETDATE()
                    WHERE idusuario = @idUsuario AND idsucursal = @idSucursalNueva
                ELSE
                    INSERT INTO dbo.usuariosucursal (idusuario, idsucursal, fechaAsignacion)
                    VALUES (@idUsuario, @idSucursalNueva, GETDATE())
                """;
            await _db.ExecuteAsync(upsert, new { idUsuario, idSucursalNueva }, tx);

            // Si la vieja era el default, actualizar usuario.idsucursal
            await _db.ExecuteAsync(
                "UPDATE dbo.usuario SET idsucursal = @idSucursalNueva WHERE idusuario = @idUsuario AND idsucursal = @idSucursalVieja",
                new { idUsuario, idSucursalVieja, idSucursalNueva }, tx);

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
