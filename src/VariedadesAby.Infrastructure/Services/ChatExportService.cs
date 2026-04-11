using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VariedadesAby.Core.Interfaces;

namespace VariedadesAby.Infrastructure.Services;

public class ChatExportService : IChatExportService
{
    static ChatExportService()
    {
        // QuestPDF Community License (gratis para ingresos < $1M USD/año)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ExportarPdf(string pregunta, string respuesta, IEnumerable<IDictionary<string, object>> datos)
    {
        var filas = datos.ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ── Header ──
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Chat Aby IA — Reporte")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                        row.ConstantItem(120).AlignRight()
                            .Text(DateTime.Now.ToString("dd/MM/yyyy HH:mm"))
                            .FontSize(9).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Blue.Darken2);
                });

                // ── Content ──
                page.Content().PaddingVertical(10).Column(col =>
                {
                    // Pregunta
                    col.Item().PaddingBottom(5).Text("Pregunta:").Bold().FontSize(11);
                    col.Item().PaddingBottom(10)
                        .Background(Colors.Grey.Lighten4).Padding(8)
                        .Text(pregunta).FontSize(10);

                    // Respuesta IA
                    col.Item().PaddingBottom(5).Text("Respuesta de Chat Aby IA:").Bold().FontSize(11);
                    col.Item().PaddingBottom(15)
                        .Background(Colors.Blue.Lighten5).Padding(8)
                        .Text(respuesta).FontSize(10);

                    // Tabla de datos
                    if (filas.Count > 0)
                    {
                        col.Item().PaddingBottom(5)
                            .Text($"Datos ({filas.Count} registros):").Bold().FontSize(11);

                        var columnas = filas[0].Keys.ToList();

                        col.Item().Table(table =>
                        {
                            // Definir columnas
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var _ in columnas)
                                    columns.RelativeColumn();
                            });

                            // Header de la tabla
                            foreach (var columna in columnas)
                            {
                                table.Cell().Background(Colors.Blue.Darken2).Padding(4)
                                    .Text(columna).FontColor(Colors.White).Bold().FontSize(8);
                            }

                            // Filas
                            var esAlterna = false;
                            foreach (var fila in filas)
                            {
                                var bgColor = esAlterna ? Colors.Grey.Lighten4 : Colors.White;
                                foreach (var columna in columnas)
                                {
                                    var valor = fila.TryGetValue(columna, out var v) ? v?.ToString() ?? "" : "";
                                    table.Cell().Background(bgColor).Padding(3)
                                        .Text(valor).FontSize(8);
                                }
                                esAlterna = !esAlterna;
                            }
                        });
                    }
                });

                // ── Footer ──
                page.Footer().AlignCenter()
                    .Text(t =>
                    {
                        t.Span("Variedades Aby — Generado por Chat Aby IA • Página ");
                        t.CurrentPageNumber();
                        t.Span(" de ");
                        t.TotalPages();
                    });
            });
        });

        return document.GeneratePdf();
    }

    public byte[] ExportarExcel(string pregunta, IEnumerable<IDictionary<string, object>> datos)
    {
        var filas = datos.ToList();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Datos");

        // Título
        worksheet.Cell(1, 1).Value = "Chat Aby IA — Reporte";
        worksheet.Cell(1, 1).Style.Font.Bold = true;
        worksheet.Cell(1, 1).Style.Font.FontSize = 14;

        // Pregunta
        worksheet.Cell(2, 1).Value = "Pregunta:";
        worksheet.Cell(2, 1).Style.Font.Bold = true;
        worksheet.Cell(2, 2).Value = pregunta;

        // Fecha
        worksheet.Cell(3, 1).Value = "Fecha:";
        worksheet.Cell(3, 1).Style.Font.Bold = true;
        worksheet.Cell(3, 2).Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");

        if (filas.Count > 0)
        {
            var columnas = filas[0].Keys.ToList();
            var startRow = 5;

            // Headers de columnas
            for (int i = 0; i < columnas.Count; i++)
            {
                var cell = worksheet.Cell(startRow, i + 1);
                cell.Value = columnas[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.DarkBlue;
                cell.Style.Font.FontColor = XLColor.White;
            }

            // Datos
            for (int rowIdx = 0; rowIdx < filas.Count; rowIdx++)
            {
                for (int colIdx = 0; colIdx < columnas.Count; colIdx++)
                {
                    var valor = filas[rowIdx].TryGetValue(columnas[colIdx], out var v) ? v : null;
                    var cell = worksheet.Cell(startRow + 1 + rowIdx, colIdx + 1);

                    switch (valor)
                    {
                        case decimal d:
                            cell.Value = d;
                            cell.Style.NumberFormat.Format = "#,##0.00";
                            break;
                        case int n:
                            cell.Value = n;
                            break;
                        case DateTime dt:
                            cell.Value = dt;
                            cell.Style.DateFormat.Format = "dd/MM/yyyy";
                            break;
                        default:
                            cell.Value = valor?.ToString() ?? "";
                            break;
                    }

                    // Zebra striping
                    if (rowIdx % 2 == 1)
                        cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                }
            }

            // Auto-ajustar ancho de columnas
            worksheet.Columns().AdjustToContents();

            // Aplicar filtros
            var lastRow = startRow + filas.Count;
            var lastCol = columnas.Count;
            worksheet.Range(startRow, 1, lastRow, lastCol).SetAutoFilter();
        }

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}
