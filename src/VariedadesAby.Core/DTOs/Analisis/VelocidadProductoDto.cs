namespace VariedadesAby.Core.DTOs.Analisis;

public class VelocidadProductoDto
{
    public string  codigo           { get; set; } = string.Empty;
    public string  articulo         { get; set; } = string.Empty;
    public string  categoria        { get; set; } = string.Empty;
    public int     stockActual      { get; set; }
    public int     vendidosUlt30d   { get; set; }
    public decimal ventasDiarias    { get; set; }   // unidades/día últimos 30 d
    public int?    diasParaAgotarse { get; set; }   // null = no se sabe / no tiene ventas
    public decimal precioVenta      { get; set; }
    public decimal precioCompra     { get; set; }
    public decimal margenPorcentaje { get; set; }

    // calculados en C#
    public string velocidad     { get; set; } = string.Empty; // Volando · Normal · Lento · Detenido
    public string semaforo      { get; set; } = string.Empty; // verde · amarillo · naranja · rojo
    public string recomendacion { get; set; } = string.Empty;
}
