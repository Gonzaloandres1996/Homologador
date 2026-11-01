using System.Diagnostics;
using System.Globalization;
using Homologador.Core.DTOs;

namespace Homologador.Core.Services;

public interface ITransformationEngine
{
    TransformationResultDto Transform(
        IEnumerable<PipelineRuleDto> rules,
        IReadOnlyList<LookupTableDto> lookups,
        IReadOnlyList<Dictionary<string, object?>> sourceRows);
}

public class TransformationEngine : ITransformationEngine
{
    private static readonly string[] SupportedDateFormats =
    [
        "dd-MM-yyyy", "dd/MM/yyyy", "d-M-yyyy", "d/M/yyyy",
        "yyyy-MM-dd", "yyyy/MM/dd", "yyyyMMdd",
        "dd-MM-yyyy HH:mm:ss", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss",
        "dd-MM-yyyyTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss"
    ];

    private static readonly CultureInfo ChileanCulture = new("es-CL");

    public TransformationResultDto Transform(
        IEnumerable<PipelineRuleDto> rules,
        IReadOnlyList<LookupTableDto> lookups,
        IReadOnlyList<Dictionary<string, object?>> sourceRows)
    {
        var sw = Stopwatch.StartNew();
        var orderedRules = rules.OrderBy(r => r.SortOrder).ToList();
        var targetColumns = orderedRules.Select(r => r.TargetColumn).Distinct().ToList();

        // Preload lookup dictionaries (case-insensitive keys) for Homologation / Lookup rules
        var lookupCache = new Dictionary<int, Dictionary<string, string>>();
        var lookupNames = new Dictionary<int, string>();

        foreach (var table in lookups)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in table.Entries)
            {
                dict[entry.KeyValue.Trim()] = entry.MappedValue;
            }
            lookupCache[table.Id] = dict;
            lookupNames[table.Id] = table.Name;
        }

        // Preload row-exclusion filter sets (comma-separated values configured in rule.Validation)
        var exclusionFilters = orderedRules
            .Where(r => !string.IsNullOrWhiteSpace(r.Validation))
            .ToDictionary(
                r => r.Id,
                r => r.Validation!
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase));

        var resultRows = new List<Dictionary<string, object?>>(sourceRows.Count);
        var warnings = new List<TransformationWarningDto>();

        for (int i = 0; i < sourceRows.Count; i++)
        {
            var sourceRow = sourceRows[i];
            var transformedRow = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            int rowNumber = i + 1;
            int warningsBeforeRow = warnings.Count;
            bool excludeRow = false;

            foreach (var rule in orderedRules)
            {
                object? targetValue = null;
                var ruleType = rule.RuleType?.Trim() ?? "Direct";

                switch (ruleType.ToUpperInvariant())
                {
                    case "FIXED":
                        targetValue = rule.FixedValue ?? string.Empty;
                        break;

                    case "DIRECT":
                    case "DATEFORMAT":
                        if (!string.IsNullOrWhiteSpace(rule.SourceColumn) && sourceRow.TryGetValue(rule.SourceColumn, out var rawVal))
                        {
                            targetValue = FormatValue(rawVal, rule.DateFormat);
                        }
                        else
                        {
                            targetValue = null;
                        }
                        break;

                    case "HOMOLOGATION":
                    case "LOOKUP":
                        string sourceKey = string.Empty;
                        if (!string.IsNullOrWhiteSpace(rule.SourceColumn) && sourceRow.TryGetValue(rule.SourceColumn, out var keyVal))
                        {
                            sourceKey = keyVal?.ToString()?.Trim() ?? string.Empty;
                        }

                        if (rule.LookupTableId.HasValue && lookupCache.TryGetValue(rule.LookupTableId.Value, out var dict))
                        {
                            if (!string.IsNullOrEmpty(sourceKey) && dict.TryGetValue(sourceKey, out var mapped))
                            {
                                targetValue = mapped;
                            }
                            else
                            {
                                var tableName = lookupNames.GetValueOrDefault(rule.LookupTableId.Value, "Unknown");
                                warnings.Add(new TransformationWarningDto(
                                    RowIndex: rowNumber,
                                    TargetColumn: rule.TargetColumn,
                                    SourceColumn: rule.SourceColumn,
                                    OffendingValue: sourceKey,
                                    Reason: $"Value '{sourceKey}' not found in lookup table '{tableName}'"
                                ));
                                targetValue = sourceKey; // Keep source as fallback
                            }
                        }
                        else
                        {
                            targetValue = sourceKey;
                        }
                        break;

                    case "EMPTY":
                        targetValue = string.Empty;
                        break;

                    case "COMPUTED":
                    case "CONDITIONAL":
                        if (!string.IsNullOrWhiteSpace(rule.SourceColumn)
                            && sourceRow.TryGetValue(rule.SourceColumn, out var compVal)
                            && compVal != null
                            && compVal != DBNull.Value
                            && !string.IsNullOrWhiteSpace(compVal.ToString()))
                        {
                            targetValue = FormatValue(compVal, rule.DateFormat);
                        }
                        else
                        {
                            targetValue = rule.FixedValue ?? string.Empty;
                        }
                        break;

                    case "FORMATTED":
                    case "SUBSTRING":
                        if (!string.IsNullOrWhiteSpace(rule.SourceColumn)
                            && sourceRow.TryGetValue(rule.SourceColumn, out var formVal)
                            && formVal != null
                            && formVal != DBNull.Value
                            && !string.IsNullOrWhiteSpace(formVal.ToString()))
                        {
                            string rawStr;
                            if (formVal is decimal decVal)
                            {
                                rawStr = decVal % 1 == 0
                                    ? ((long)decVal).ToString(CultureInfo.InvariantCulture)
                                    : decVal.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (formVal is double dblVal)
                            {
                                rawStr = dblVal % 1 == 0
                                    ? ((long)dblVal).ToString(CultureInfo.InvariantCulture)
                                    : dblVal.ToString(CultureInfo.InvariantCulture);
                            }
                            else if (formVal is float fltVal)
                            {
                                rawStr = fltVal % 1 == 0
                                    ? ((long)fltVal).ToString(CultureInfo.InvariantCulture)
                                    : fltVal.ToString(CultureInfo.InvariantCulture);
                            }
                            else
                            {
                                rawStr = formVal.ToString()!.Trim();
                            }

                            string segment;

                            if (rule.SubstringStart.HasValue && rule.SubstringStart.Value > 0)
                            {
                                int startIndex = rule.SubstringStart.Value - 1; // 1-based index (Excel MID style)
                                if (startIndex >= rawStr.Length)
                                {
                                    segment = string.Empty;
                                }
                                else if (rule.SubstringLength.HasValue && rule.SubstringLength.Value > 0)
                                {
                                    int len = Math.Min(rule.SubstringLength.Value, rawStr.Length - startIndex);
                                    segment = rawStr.Substring(startIndex, len);
                                }
                                else
                                {
                                    segment = rawStr.Substring(startIndex);
                                }
                            }
                            else
                            {
                                if (rule.SubstringLength.HasValue && rule.SubstringLength.Value > 0)
                                {
                                    int targetLen = rule.SubstringLength.Value;
                                    if (rawStr.Length > targetLen)
                                    {
                                        string discardedPrefix = rawStr.Substring(0, rawStr.Length - targetLen);
                                        if (discardedPrefix.Any(c => c != '0'))
                                        {
                                            warnings.Add(new TransformationWarningDto(
                                                RowIndex: rowNumber,
                                                TargetColumn: rule.TargetColumn,
                                                SourceColumn: rule.SourceColumn,
                                                OffendingValue: rawStr,
                                                Reason: $"El valor del lote '{rawStr}' excede los {targetLen} dígitos y se truncaron dígitos significativos."
                                            ));
                                        }
                                    }

                                    int takeLen = Math.Min(targetLen, rawStr.Length);
                                    segment = rawStr.Substring(rawStr.Length - takeLen, takeLen);
                                }
                                else
                                {
                                    segment = rawStr;
                                }
                            }

                            if (rule.SubstringLength.HasValue && rule.SubstringLength.Value > 0 &&
                                string.Equals(ruleType, "Formatted", StringComparison.OrdinalIgnoreCase))
                            {
                                segment = segment.PadLeft(rule.SubstringLength.Value, '0');
                            }

                            targetValue = (rule.FixedValue ?? string.Empty) + segment;
                        }
                        else
                        {
                            targetValue = null;
                        }
                        break;

                    case "CONCAT":
                        var concatCols = (rule.SourceColumn ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var sep = rule.FixedValue ?? string.Empty;
                        var parts = new List<string>();
                        foreach (var cName in concatCols)
                        {
                            if (sourceRow.TryGetValue(cName, out var cVal) && cVal != null && cVal != DBNull.Value)
                            {
                                parts.Add(cVal.ToString()!.Trim());
                            }
                        }
                        targetValue = string.Join(sep, parts);
                        break;

                    case "AGGREGATE":
                        var colNames = (rule.SourceColumn ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        int n = colNames.Length;
                        decimal sum = 0m;

                        foreach (var colName in colNames)
                        {
                            object? rawColVal = null;
                            if (sourceRow.TryGetValue(colName, out var foundVal))
                            {
                                rawColVal = foundVal;
                            }
                            sum += ParseDecimalValue(rawColVal, colName, rule.TargetColumn, rowNumber, warnings);
                        }

                        bool isAvg = string.Equals(rule.FixedValue?.Trim(), "AVG", StringComparison.OrdinalIgnoreCase);
                        decimal aggResult = isAvg ? (n > 0 ? (sum / n) : 0m) : sum;

                        if (rule.SubstringLength.HasValue && rule.SubstringLength.Value >= 0)
                        {
                            aggResult = Math.Round(aggResult, rule.SubstringLength.Value, MidpointRounding.AwayFromZero);
                        }

                        targetValue = aggResult;
                        break;
                }

                // Row-level exclusion filter: match against either transformed target value or raw source value
                if (exclusionFilters.TryGetValue(rule.Id, out var excludedValues) && excludedValues.Count > 0)
                {
                    var targetStr = targetValue?.ToString()?.Trim();
                    var rawSourceStr = !string.IsNullOrWhiteSpace(rule.SourceColumn) && sourceRow.TryGetValue(rule.SourceColumn, out var srcVal)
                        ? srcVal?.ToString()?.Trim()
                        : null;

                    if ((!string.IsNullOrEmpty(targetStr) && excludedValues.Contains(targetStr)) ||
                        (!string.IsNullOrEmpty(rawSourceStr) && excludedValues.Contains(rawSourceStr)))
                    {
                        warnings.RemoveRange(warningsBeforeRow, warnings.Count - warningsBeforeRow);
                        excludeRow = true;
                        break;
                    }
                }

                // Check required validation
                if (rule.IsRequired && (targetValue == null || string.IsNullOrWhiteSpace(targetValue.ToString())))
                {
                    warnings.Add(new TransformationWarningDto(
                        RowIndex: rowNumber,
                        TargetColumn: rule.TargetColumn,
                        SourceColumn: rule.SourceColumn,
                        OffendingValue: string.Empty,
                        Reason: $"Required column '{rule.TargetColumn}' is empty"
                    ));
                }

                transformedRow[rule.TargetColumn] = targetValue;
            }

            if (!excludeRow)
            {
                resultRows.Add(transformedRow);
            }
        }

        sw.Stop();

        return new TransformationResultDto(
            TotalRows: resultRows.Count,
            WarningCount: warnings.Count,
            Columns: targetColumns,
            Rows: resultRows,
            Warnings: warnings,
            DurationMs: sw.ElapsedMilliseconds
        );
    }

    private static object? FormatValue(object? val, string? dateFormat)
    {
        if (val == null) return null;

        if (!string.IsNullOrWhiteSpace(dateFormat))
        {
            if (val is DateTime dt)
            {
                return dt.ToString(dateFormat, CultureInfo.InvariantCulture);
            }
            if (val is DateOnly dOnly)
            {
                return dOnly.ToString(dateFormat, CultureInfo.InvariantCulture);
            }

            var valStr = val.ToString()?.Trim();
            if (!string.IsNullOrEmpty(valStr))
            {
                if (DateTime.TryParseExact(valStr, SupportedDateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDt) ||
                    DateTime.TryParse(valStr, ChileanCulture, DateTimeStyles.None, out parsedDt) ||
                    DateTime.TryParse(valStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDt) ||
                    DateTime.TryParse(valStr, out parsedDt))
                {
                    return parsedDt.ToString(dateFormat, CultureInfo.InvariantCulture);
                }
            }
        }

        return val;
    }

    private static decimal ParseDecimalValue(object? val, string colName, string targetColumn, int rowNumber, List<TransformationWarningDto> warnings)
    {
        if (val == null || val == DBNull.Value) return 0m;

        switch (val)
        {
            case decimal dec:
                return dec;
            case double dbl when !double.IsNaN(dbl) && !double.IsInfinity(dbl):
                return (decimal)dbl;
            case float flt when !float.IsNaN(flt) && !float.IsInfinity(flt):
                return (decimal)flt;
            case int i:
                return i;
            case long l:
                return l;
            case short s:
                return s;
            case byte b:
                return b;
            case sbyte sb:
                return sb;
            case uint ui:
                return ui;
            case ulong ul:
                return ul;
            case ushort us:
                return us;
        }

        var str = val.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str))
        {
            return 0m;
        }

        var numStr = str;
        if (!numStr.Contains('.') && numStr.Contains(','))
        {
            numStr = numStr.Replace(',', '.');
        }

        if (decimal.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        warnings.Add(new TransformationWarningDto(
            RowIndex: rowNumber,
            TargetColumn: targetColumn,
            SourceColumn: colName,
            OffendingValue: str,
            Reason: $"Valor '{str}' en columna '{colName}' no es un número válido; se asumió 0."
        ));

        return 0m;
    }
}
