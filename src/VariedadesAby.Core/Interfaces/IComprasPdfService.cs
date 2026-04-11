using VariedadesAby.Core.DTOs.Compras;

namespace VariedadesAby.Core.Interfaces;

public interface IComprasPdfService
{
    Task<AnalizarPdfResponseDto> AnalizarPdfAsync(
        Stream pdfStream,
        int idProveedor,
        int idUsuario,
        int idSucursal,
        decimal tipoCambio);
}
