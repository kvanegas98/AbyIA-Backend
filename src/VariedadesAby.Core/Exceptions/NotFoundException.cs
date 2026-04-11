namespace VariedadesAby.Core.Exceptions;

/// <summary>
/// Excepción para recursos que no existen.
/// Se traduce a 404 Not Found con mensaje legible para el usuario.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string mensaje) : base(mensaje) { }
}
