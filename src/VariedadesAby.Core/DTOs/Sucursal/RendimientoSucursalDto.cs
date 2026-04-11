namespace VariedadesAby.Core.DTOs.Sucursal;

public class RendimientoSucursalDto
{
    public int     idsucursal       { get; set; }
    public string  sucursal         { get; set; } = string.Empty;

    // Periodo actual
    public int     totalVentas      { get; set; }
    public decimal ventaTotal       { get; set; }
    public decimal utilidadTotal    { get; set; }
    public decimal ticketPromedio   { get; set; }
    public int     unidadesVendidas { get; set; }

    // Periodo anterior (mismo rango de días)
    public decimal ventaAnterior    { get; set; }

    // Rango de fechas (para que el frontend muestre qué períodos se comparan)
    public DateTime fechaInicioActual   { get; set; }
    public DateTime fechaFinActual      { get; set; }
    public DateTime fechaInicioAnterior { get; set; }
    public DateTime fechaFinAnterior    { get; set; }

    // Calculados en C#
    public decimal variacion        { get; set; } // % cambio vs periodo anterior
    public string  tendencia        { get; set; } = string.Empty; // sube · baja · estable
    public string  semaforo         { get; set; } = string.Empty; // verde · rojo · gris
}
