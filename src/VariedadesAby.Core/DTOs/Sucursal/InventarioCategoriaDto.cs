namespace VariedadesAby.Core.DTOs.Sucursal;

public class InventarioCategoriaDto
{
    public string  categoria            { get; set; } = string.Empty;
    public int     articulos            { get; set; }
    public int     totalUnidades        { get; set; }
    public decimal valorCosto           { get; set; }
    public decimal valorVenta           { get; set; }
    public decimal gananciasPotenciales { get; set; }
    public decimal margenPotencial      { get; set; } // %
    public decimal porcentajeDelTotal   { get; set; } // % sobre valorCosto total
}
