
namespace VariedadesAby.Core.DTOs.Finanzas;

public class FinanzasResumenDto
{
    public decimal TotalCartera { get; set; }
    public decimal CobrosProyectados7d { get; set; }
    public int ClientesMorosos { get; set; }
}

public class FinanzasFlujoCajaDto
{
    public DateTime Fecha { get; set; }
    public decimal Ingresos { get; set; }
    public decimal Egresos { get; set; }
}
