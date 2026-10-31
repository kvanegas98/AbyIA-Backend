namespace VariedadesAby.Core.DTOs.Ventas;

public class BuscarVentasResponseDto
{
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }
    public int TotalPaginas => (int)Math.Ceiling((double)Total / TamanoPagina);
    public IEnumerable<VentaResumenDto> Datos { get; set; } = [];
}
