using System.Globalization;
using Homologador.Core.DTOs;

namespace Homologador.Core.Services;

public static class RowIdentityExtractor
{
    public static string ExtractPrimaryKey(
        string targetArea,
        IReadOnlyList<PipelineRuleDto> rules,
        IReadOnlyDictionary<string, object?> transformedRow,
        IReadOnlyDictionary<string, object?>? sourceRow = null)
    {
        // 1. Check all rules marked as IsIdentity dynamically (supports single or composite keys)
        var identityRules = rules.Where(r => r.IsIdentity).OrderBy(r => r.SortOrder).ToList();
        if (identityRules.Count > 0)
        {
            var parts = new List<string>();
            foreach (var rule in identityRules)
            {
                string? str = null;
                if (transformedRow.TryGetValue(rule.TargetColumn, out var idVal) && idVal != null)
                {
                    str = idVal.ToString()?.Trim();
                }
                else if (sourceRow != null && !string.IsNullOrWhiteSpace(rule.SourceColumn) &&
                         sourceRow.TryGetValue(rule.SourceColumn, out var rawIdVal) && rawIdVal != null)
                {
                    str = rawIdVal.ToString()?.Trim();
                }

                if (!string.IsNullOrEmpty(str))
                {
                    parts.Add(str);
                }
            }

            if (parts.Count > 0)
            {
                return string.Join("|", parts);
            }
        }

        // 2. Area-specific fallback target columns
        string[] candidateTargets = targetArea.StartsWith("Producc", StringComparison.OrdinalIgnoreCase)
            ? ["LOTEPROCESO", "LOTERECEPCION", "LoteProceso", "Lote"]
            : targetArea.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase)
                ? ["PARTIDA", "Lote", "LOTE"]
                : ["Lote", "LOTE", "PARTIDA"];

        foreach (var col in candidateTargets)
        {
            if (transformedRow.TryGetValue(col, out var val) && val != null)
            {
                var str = val.ToString()?.Trim();
                if (!string.IsNullOrEmpty(str)) return str;
            }
        }

        if (sourceRow != null)
        {
            string[] candidateSources = ["Lote", "Nro. Proceso", "LoteProceso", "Partida"];
            foreach (var col in candidateSources)
            {
                if (sourceRow.TryGetValue(col, out var val) && val != null)
                {
                    var str = val.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(str)) return str;
                }
            }
        }

        return string.Empty;
    }

    public static GeneratedRowItemDto ExtractRowMetadata(
        string targetArea,
        IReadOnlyList<PipelineRuleDto> rules,
        IReadOnlyDictionary<string, object?> transformedRow,
        IReadOnlyDictionary<string, object?>? sourceRow = null)
    {
        var primaryKey = ExtractPrimaryKey(targetArea, rules, transformedRow, sourceRow);

        // Productor, Kg and Calibre MUST come strictly from the pipeline's transformed output (transformedRow)
        // whenever the pipeline defines that target column, never falling back to a different raw source column.
        var productor = FindFromPipelineOutput(transformedRow, ["Productor", "PRODUCTOR"]);
        if (string.IsNullOrWhiteSpace(productor) && sourceRow != null)
        {
            productor = FindFromPipelineOutput(sourceRow, ["Productor", "PRODUCTOR", "NombreProductor", "NOM_PRODUCTOR", "DESC_PRODUCTOR"]);
        }

        decimal? kg = FindDecimalFromPipelineOutput(transformedRow,
            ["Kilos", "KILOS", "PESOTOTALMUESTRA", "KILOS RECEPCION", "KilosNeto", "PesoNeto"]);
        if (!kg.HasValue && sourceRow != null)
        {
            kg = FindDecimalFromPipelineOutput(sourceRow,
                ["Kilos", "KILOS", "PESOTOTALMUESTRA", "KILOS RECEPCION", "KilosNeto", "PesoNeto"]);
        }

        var calibre = FindFromPipelineOutput(transformedRow,
            ["CALIBRE", "Calibre"]);
        if (string.IsNullOrWhiteSpace(calibre) && sourceRow != null)
        {
            calibre = FindFromPipelineOutput(sourceRow,
                ["CALIBRE", "Calibre", "CodCalibre"]);
        }

        return new GeneratedRowItemDto(
            PrimaryKey: primaryKey,
            Productor: productor,
            Kg: kg,
            Calibre: calibre
        );
    }

    private static string? FindFromPipelineOutput(
        IReadOnlyDictionary<string, object?> transformedRow,
        string[] targetCandidates)
    {
        foreach (var key in targetCandidates)
        {
            if (transformedRow.TryGetValue(key, out var val))
            {
                return val?.ToString()?.Trim() ?? string.Empty;
            }
        }

        return null;
    }

    private static decimal? FindDecimalFromPipelineOutput(
        IReadOnlyDictionary<string, object?> transformedRow,
        string[] targetCandidates)
    {
        foreach (var key in targetCandidates)
        {
            if (transformedRow.TryGetValue(key, out var val))
            {
                return TryParseDecimal(val, out var d) ? d : null;
            }
        }

        return null;
    }

    private static bool TryParseDecimal(object? val, out decimal result)
    {
        result = 0m;
        if (val == null || val == DBNull.Value) return false;

        switch (val)
        {
            case decimal d:
                result = d;
                return true;
            case double dbl when !double.IsNaN(dbl) && !double.IsInfinity(dbl):
                result = (decimal)dbl;
                return true;
            case float flt when !float.IsNaN(flt) && !float.IsInfinity(flt):
                result = (decimal)flt;
                return true;
            case int i:
                result = i;
                return true;
            case long l:
                result = l;
                return true;
        }

        var str = val.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return false;
        if (!str.Contains('.') && str.Contains(',')) str = str.Replace(',', '.');

        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    public static List<Dictionary<string, object?>> BuildOfflineDemoRows(
        string targetArea,
        IReadOnlyList<PipelineRuleDto> rules,
        int startIndex = 1)
    {
        var rows = new List<Dictionary<string, object?>>();
        var today = DateTime.Now.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

        for (int i = 0; i < 3; i++)
        {
            int seq = startIndex + i;
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.SourceColumn)) continue;
                var cols = rule.SourceColumn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var col in cols)
                {
                    if (row.ContainsKey(col)) continue;

                    if (col.Equals("Lote", StringComparison.OrdinalIgnoreCase))
                        row[col] = $"2026{seq:D4}";
                    else if (col.Equals("Nro. Proceso", StringComparison.OrdinalIgnoreCase))
                        row[col] = $"{seq:D5}";
                    else if (col.Contains("Productor", StringComparison.OrdinalIgnoreCase))
                        row[col] = $"PROD-{100 + seq}";
                    else if (col.Contains("Especie", StringComparison.OrdinalIgnoreCase))
                        row[col] = "Cereza";
                    else if (col.Contains("Variedad", StringComparison.OrdinalIgnoreCase))
                        row[col] = "Santina";
                    else if (col.Contains("Calibre", StringComparison.OrdinalIgnoreCase))
                        row[col] = i switch { 0 => "XL", 1 => "2J", _ => "3J" };
                    else if (col.Contains("Peso", StringComparison.OrdinalIgnoreCase) || col.Contains("Kilo", StringComparison.OrdinalIgnoreCase))
                        row[col] = 1250.50m + (i * 180m);
                    else if (col.Contains("Fecha", StringComparison.OrdinalIgnoreCase))
                        row[col] = today;
                    else if (col.Contains("Esponja", StringComparison.OrdinalIgnoreCase) || col.Contains("Polvo", StringComparison.OrdinalIgnoreCase))
                        row[col] = "no";
                    else if (col.Contains("Tratamiento", StringComparison.OrdinalIgnoreCase))
                        row[col] = "Si";
                    else
                        row[col] = 10;
                }
            }

            rows.Add(row);
        }

        return rows;
    }
}
