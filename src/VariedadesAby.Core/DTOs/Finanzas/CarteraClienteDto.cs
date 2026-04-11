namespace VariedadesAby.Core.DTOs.Finanzas;

/// <summary>
/// Resumen de cartera de un cliente con crédito activo.
/// Pendiente = TotalAPagar - TotalAbonado - TotalNotaCredito
/// </summary>
public record CarteraClienteDto
{
    public int IdCredito { get; init; }
    public int IdPersona { get; init; }
    public string NombreCliente { get; init; } = string.Empty;
    public string? Telefono { get; init; }
    public decimal TotalAPagar { get; init; }
    public decimal TotalAbonado { get; init; }
    public decimal TotalNotaCredito { get; init; }
    public decimal Pendiente { get; init; }
    public DateTime? FechaUltimoAbono { get; init; }
    public int CantidadAbonos { get; init; }
    public int CantidadVentas { get; init; }
    public bool Alerta { get; init; }
    public DateTime? PrimerCredito { get; init; }
}
