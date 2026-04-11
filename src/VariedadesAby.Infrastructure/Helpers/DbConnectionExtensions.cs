using System.Data;

namespace VariedadesAby.Infrastructure.Helpers;

/// <summary>
/// Extensiones de IDbConnection para manejar transacciones con Dapper
/// de forma consistente y sin perder el stack trace original.
/// </summary>
public static class DbConnectionExtensions
{
    /// <summary>
    /// Ejecuta una operación dentro de una transacción y retorna un valor.
    /// Hace Commit si todo sale bien, Rollback si lanza excepción.
    /// </summary>
    public static async Task<T> WithTransactionAsync<T>(
        this IDbConnection connection,
        Func<IDbTransaction, Task<T>> action)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var tx = connection.BeginTransaction();
        try
        {
            var result = await action(tx);
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw; // Re-lanza la excepción original sin perder tipo ni stack trace
        }
    }

    /// <summary>
    /// Ejecuta una operación dentro de una transacción sin valor de retorno.
    /// Hace Commit si todo sale bien, Rollback si lanza excepción.
    /// </summary>
    public static async Task WithTransactionAsync(
        this IDbConnection connection,
        Func<IDbTransaction, Task> action)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        using var tx = connection.BeginTransaction();
        try
        {
            await action(tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
