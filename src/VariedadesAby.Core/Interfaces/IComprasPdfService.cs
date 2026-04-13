using VariedadesAby.Core.DTOs.Compras;

namespace VariedadesAby.Core.Interfaces;

public interface IComprasPdfService
{
    Task<AnalizarPdfResponseDto> AnalizarPdfAsync(
        Stream pdfStream,
        int idProveedor,
        int idUsuario,
        int idSucursal);

    /// <summary>
    /// Busca un artículo por código. Retorna esNuevo=true si no existe en la BD.
    /// </summary>
    Task<ValidarArticuloDto> ValidarArticuloAsync(string codigo);
}
