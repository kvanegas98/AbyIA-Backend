namespace VariedadesAby.Core.DTOs.Vendedores;

public class FiltroVendedoresViewModel
{
    public DateTime FechaInicio { get; set; } = DateTime.Today.AddDays(-30);
    public DateTime FechaFin { get; set; } = DateTime.Today;
}
