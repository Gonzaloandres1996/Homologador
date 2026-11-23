using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
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
        string fileNamePattern,
        bool normalizeForExcelCompatibility = false);
}

public class FileGeneratorService : IFileGeneratorService
{
    public GeneratedFileResult GenerateFile(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string outputFormat,
        string? sheetName,
        string fileNamePattern,
        bool normalizeForExcelCompatibility = false)
    {
        var resolvedFileName = ResolveFileName(fileNamePattern, outputFormat);

        return outputFormat?.Trim().ToUpperInvariant() switch
        {
            "CSV" => GenerateDelimited(columns, rows, ";", resolvedFileName, "text/csv; charset=utf-8"),
            "TABDELIMITED" or "TSV" => GenerateDelimited(columns, rows, "\t", resolvedFileName, "text/tab-separated-values; charset=utf-8"),
            _ => GenerateExcel(columns, rows, sheetName, resolvedFileName, normalizeForExcelCompatibility)
        };
    }

    private static GeneratedFileResult GenerateExcel(
        IReadOnlyList<string> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string? sheetName,
        string fileName,
        bool normalizeForExcelCompatibility)
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

        var fileBytes = normalizeForExcelCompatibility
            ? NormalizeExcelPackage(ms.ToArray())
            : ms.ToArray();

        return new GeneratedFileResult(
            FileBytes: fileBytes,
            ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            FileName: fileName
        );
    }

    private const string SpreadsheetMainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string ExtendedPropertiesNs = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
    private const string PackageRelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    /// <summary>
    /// Normalizes a ClosedXML-generated .xlsx package to match Microsoft Excel's OOXML conventions:
    /// removes UTF-8 BOMs, converts absolute relationship targets to relative paths, and promotes
    /// prefixed SpreadsheetML/ExtendedProperties namespaces (x:, ap:) to default namespaces.
    /// </summary>
    public static byte[] NormalizeExcelPackage(byte[] xlsxBytes)
    {
        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var xmlSettings = new XmlWriterSettings
        {
            Encoding = utf8NoBom,
            Indent = false,
            OmitXmlDeclaration = false,
            NewLineHandling = NewLineHandling.None
        };

        using var inputStream = new MemoryStream(xlsxBytes, writable: false);
        using var inputArchive = new ZipArchive(inputStream, ZipArchiveMode.Read);
        using var outputStream = new MemoryStream(xlsxBytes.Length);

        using (var outputArchive = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in inputArchive.Entries)
            {
                var outEntry = outputArchive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var inEntryStream = entry.Open();
                using var outEntryStream = outEntry.Open();

                var normalizedName = entry.FullName.Replace('\\', '/');
                if (IsXmlPart(normalizedName))
                {
                    var doc = XDocument.Load(inEntryStream, System.Xml.Linq.LoadOptions.PreserveWhitespace);

                    if (normalizedName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    {
                        NormalizeRelationships(doc, normalizedName);
                    }
                    else if (normalizedName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
                    {
                        PromoteNamespaceToDefault(doc, SpreadsheetMainNs);
                    }
                    else if (normalizedName.Equals("docProps/app.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        PromoteNamespaceToDefault(doc, ExtendedPropertiesNs);
                    }

                    doc.Declaration = new XDeclaration("1.0", "UTF-8", "yes");

                    using var writer = XmlWriter.Create(outEntryStream, xmlSettings);
                    doc.Save(writer);
                }
                else
                {
                    inEntryStream.CopyTo(outEntryStream);
                }
            }
        }

        return outputStream.ToArray();
    }

    private static bool IsXmlPart(string entryName) =>
        entryName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        || entryName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)
        || entryName.EndsWith(".psmdcp", StringComparison.OrdinalIgnoreCase);

    private static void PromoteNamespaceToDefault(XDocument doc, string targetNamespace)
    {
        var root = doc.Root;
        if (root == null) return;

        var prefixedDecls = root.Attributes()
            .Where(a => a.IsNamespaceDeclaration && a.Name.Namespace == XNamespace.Xmlns && a.Value == targetNamespace)
            .ToList();

        if (prefixedDecls.Count == 0) return;

        foreach (var attr in prefixedDecls)
        {
            attr.Remove();
        }

        if (root.Attribute("xmlns") == null)
        {
            root.Add(new XAttribute("xmlns", targetNamespace));
        }

        // Clean any redundant prefixed declarations on descendant elements.
        foreach (var el in root.Descendants())
        {
            var redundant = el.Attributes()
                .Where(a => a.IsNamespaceDeclaration && a.Name.Namespace == XNamespace.Xmlns && a.Value == targetNamespace)
                .ToList();
            foreach (var attr in redundant)
            {
                attr.Remove();
            }
        }
    }

    private static void NormalizeRelationships(XDocument doc, string relsEntryName)
    {
        var root = doc.Root;
        if (root == null) return;

        var ownerDir = GetRelsOwnerDirectory(relsEntryName);
        XName relationshipName = XName.Get("Relationship", PackageRelationshipsNs);

        foreach (var rel in root.Elements(relationshipName))
        {
            var targetMode = rel.Attribute("TargetMode")?.Value;
            if (string.Equals(targetMode, "External", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetAttr = rel.Attribute("Target");
            if (targetAttr == null || !targetAttr.Value.StartsWith('/'))
            {
                continue;
            }

            var targetWithoutSlash = targetAttr.Value.TrimStart('/');
            if (ownerDir.Length == 0)
            {
                targetAttr.Value = targetWithoutSlash;
            }
            else if (targetWithoutSlash.StartsWith(ownerDir, StringComparison.OrdinalIgnoreCase))
            {
                targetAttr.Value = targetWithoutSlash[ownerDir.Length..];
            }
            else
            {
                var baseUri = new Uri("https://ooxml.local/" + ownerDir);
                var targetUri = new Uri("https://ooxml.local/" + targetWithoutSlash);
                targetAttr.Value = baseUri.MakeRelativeUri(targetUri).ToString();
            }
        }
    }

    private static string GetRelsOwnerDirectory(string relsEntryName)
    {
        const string relsSegment = "_rels/";
        var idx = relsEntryName.LastIndexOf(relsSegment, StringComparison.OrdinalIgnoreCase);
        return idx <= 0 ? string.Empty : relsEntryName[..idx];
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
