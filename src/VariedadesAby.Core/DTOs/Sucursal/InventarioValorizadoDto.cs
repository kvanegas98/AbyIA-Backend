namespace VariedadesAby.Core.DTOs.Sucursal;

public class InventarioValorizadoResultadoDto
{
    public InventarioValorizadoResumenDto        resumen   { get; set; } = new();
    public IEnumerable<InventarioSucursalDto>    sucursales { get; set; } = [];
}

public class InventarioValorizadoResumenDto
{
    public int     totalSucursales         { get; set; }
    public int     totalArticulosDistintos { get; set; }
    public int     totalUnidades           { get; set; }
    public decimal valorTotalCosto         { get; set; }
    public decimal valorTotalVenta         { get; set; }
    public decimal gananciasPotenciales    { get; set; }
    public decimal margenPotencial         { get; set; } // %
}

public class InventarioSucursalDto
{
    public int     idsucursal           { get; set; }
    public string  sucursal             { get; set; } = string.Empty;
    public int     articulos            { get; set; }
    public int     totalUnidades        { get; set; }
    public decimal valorCosto           { get; set; }
    public decimal valorVenta           { get; set; }
    public decimal gananciasPotenciales { get; set; }
    public decimal margenPotencial      { get; set; } // %
    public decimal porcentajeDelTotal   { get; set; } // % sobre valorCosto total
}
