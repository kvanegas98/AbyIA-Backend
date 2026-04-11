namespace VariedadesAby.Core.Exceptions;

/// <summary>
/// Excepción de negocio para el módulo de ingresos.
/// Se traduce a 400 Bad Request con mensaje legible para el usuario.
/// </summary>
public class IngresoException : Exception
{
    public IngresoException(string mensaje) : base(mensaje) { }
}
