namespace VariedadesAby.Core.Interfaces;

/// <summary>
/// Servicio de exportación de datos del Chat Aby IA a PDF y Excel
/// </summary>
public interface IChatExportService
{
    byte[] ExportarPdf(string pregunta, string respuesta, IEnumerable<IDictionary<string, object>> datos);
    byte[] ExportarExcel(string pregunta, IEnumerable<IDictionary<string, object>> datos);
}
