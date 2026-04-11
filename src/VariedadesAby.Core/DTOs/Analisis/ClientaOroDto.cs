namespace VariedadesAby.Core.DTOs.Analisis;

public class ClientaOroDto
{
    public int    idpersona      { get; set; }
    public string nombre         { get; set; } = string.Empty;
    public string telefono       { get; set; } = string.Empty;
    public int    totalCompras   { get; set; }
    public decimal totalGastado  { get; set; }
    public decimal ticketPromedio { get; set; }
    public DateTime? ultimaCompra { get; set; }
    public int    diasSinComprar  { get; set; }

    // calculados en C#
    public string categoria   { get; set; } = string.Empty; // VIP · Frecuente · Ocasional
    public string semaforo    { get; set; } = string.Empty; // verde · amarillo · rojo
    public string alerta      { get; set; } = string.Empty;
}
