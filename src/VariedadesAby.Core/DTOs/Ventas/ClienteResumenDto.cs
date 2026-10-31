namespace VariedadesAby.Core.DTOs.Ventas;

public class ClienteResumenDto
{
    public int IdCliente { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
}
