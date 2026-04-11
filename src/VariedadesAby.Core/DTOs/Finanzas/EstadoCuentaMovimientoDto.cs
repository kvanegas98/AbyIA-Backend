namespace VariedadesAby.Core.DTOs.Finanzas;

/// <summary>
/// Movimiento del estado de cuenta de un cliente (Factura/Abono/Nota de Crédito)
/// </summary>
public record EstadoCuentaMovimientoDto
{
    public DateTime? FechaHora { get; init; }
    public string TipoMovimiento { get; init; } = string.Empty;
    public int IdCliente { get; init; }
    public int? IdCredito { get; init; }
    public string? NumDocumento { get; init; }
    public int? IdEstado { get; init; }
    public decimal MontoDebito { get; init; }
    public decimal MontoCredito { get; init; }
    public decimal Saldo { get; init; }
}
