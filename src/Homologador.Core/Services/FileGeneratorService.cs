using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Homologador.Core.DTOs;

namespace Homologador.Core.Services;

public interface IFileGeneratorService
{
    GeneratedFileResult GenerateFile(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string outputFormat,
        string? sheetName,
        string fileNamePattern);
}

public class FileGeneratorService : IFileGeneratorService
{
    public GeneratedFileResult GenerateFile(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string outputFormat,
        string? sheetName,
        string fileNamePattern)
    {
        var resolvedFileName = ResolveFileName(fileNamePattern, outputFormat);

        return outputFormat?.Trim().ToUpperInvariant() switch
        {
            "CSV" => GenerateDelimited(columns, rows, ";", resolvedFileName, "text/csv; charset=utf-8"),
            "TABDELIMITED" or "TSV" => GenerateDelimited(columns, rows, "\t", resolvedFileName, "text/tab-separated-values; charset=utf-8"),
            _ => GenerateExcel(columns, rows, sheetName, resolvedFileName)
        };
    }

    private static GeneratedFileResult GenerateExcel(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string? sheetName,
        string fileName)
    {
        using var workbook = new XLWorkbook();
        var safeSheetName = string.IsNullOrWhiteSpace(sheetName) ? "Data" : sheetName.Trim();
        if (safeSheetName.Length > 31) safeSheetName = safeSheetName[..31];

        var worksheet = workbook.Worksheets.Add(safeSheetName);

        for (int c = 0; c < columns.Count; c++)
        {
            var cell = worksheet.Cell(1, c + 1);
            cell.Value = columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(220, 230, 242);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(180, 195, 215);
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        for (int r = 0; r < rows.Count; r++)
        {
            var rowDict = rows[r];
            int excelRow = r + 2;

            for (int c = 0; c < columns.Count; c++)
            {
                var colName = columns[c];
                var cell = worksheet.Cell(excelRow, c + 1);

                if (rowDict.TryGetValue(colName, out var val) && val != null)
                {
                    SetCellValue(cell, val);
                }
                else
                {
                    cell.Value = string.Empty;
                }

                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromArgb(230, 230, 230);
            }
        }

        worksheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);

        return new GeneratedFileResult(
            FileBytes: ms.ToArray(),
            ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName: fileName
        );
    }

    private static void SetCellValue(IXLCell cell, object val)
    {
        switch (val)
        {
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case double d:
                cell.Value = d;
                break;
            case decimal m:
                cell.Value = m;
                break;
            case float f:
                cell.Value = f;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-MM-dd";
                break;
            case bool b:
                cell.Value = b;
                break;
            default:
                cell.Value = val.ToString() ?? string.Empty;
                break;
        }
    }

    private static GeneratedFileResult GenerateDelimited(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string delimiter,
        string fileName,
        string contentType)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(delimiter, columns.Select(EscapeDelimitedField)));

        foreach (var row in rows)
        {
            var lineValues = new List<string>(columns.Count);
            foreach (var col in columns)
            {
                if (row.TryGetValue(col, out var val) && val != null)
                {
                    lineValues.Add(EscapeDelimitedField(val.ToString() ?? string.Empty));
                }
                else
                {
                    lineValues.Add(string.Empty);
                }
            }
            sb.AppendLine(string.Join(delimiter, lineValues));
        }

        var utf8WithBom = new UTF8Encoding(true);
        using var ms = new MemoryStream();
        using (var writer = new StreamWriter(ms, utf8WithBom))
        {
            writer.Write(sb.ToString());
        }

        return new GeneratedFileResult(ms.ToArray(), contentType, fileName);
    }

    private static string EscapeDelimitedField(string field)
    {
        if (field.Contains(';') || field.Contains(',') || field.Contains('\t') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }

    public static string ResolveFileName(string pattern, string outputFormat)
    {
        var now = DateTime.Now;
        var name = string.IsNullOrWhiteSpace(pattern) ? "REPORT_{yyyyMMdd_HHmmss}" : pattern.Trim();

        name = name.Replace("{yyyyMMdd_HHmmss}", now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture))
                   .Replace("{yyyy-MM-dd}", now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                   .Replace("{yyyyMMdd}", now.ToString("yyyyMMdd", CultureInfo.InvariantCulture))
                   .Replace("{HHmmss}", now.ToString("HHmmss", CultureInfo.InvariantCulture));

        var ext = outputFormat?.Trim().ToUpperInvariant() switch
        {
            "CSV" => ".csv",
            "TABDELIMITED" or "TSV" => ".tsv",
            _ => ".xlsx"
        };

        if (!name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
        {
            name += ext;
        }

        return name;
    }
}
