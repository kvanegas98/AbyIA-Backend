namespace VariedadesAby.Core.DTOs.Finanzas;

/// <summary>
/// Detalle de un abono con información de deuda asociada
/// </summary>
public record AbonoDetalleDto
{
    public int IdAbono { get; init; }
    public string NombreCliente { get; init; } = string.Empty;
    public int IdCliente { get; init; }
    public int IdUsuario { get; init; }
    public string Usuario { get; init; } = string.Empty;
    public DateTime Fecha { get; init; }
    public decimal MontoAbono { get; init; }
    public string? Codigo { get; init; }
    public decimal Pendiente { get; init; }
    public int IdEstado { get; init; }
    public string? TipoPago { get; init; }
    public string? DescripcionPago { get; init; }
    public decimal MontoAbonado { get; init; }
    public decimal MontoNotaCredito { get; init; }
    public decimal DeudaInicial { get; init; }
    public decimal DeudaPendiente { get; init; }
}
