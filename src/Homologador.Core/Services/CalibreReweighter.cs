using System.Globalization;
using System.Text;

namespace Homologador.Core.Services;

public sealed record ReweightResult(decimal[] Values, string? Warning);

public sealed record CalibreToleranceIssue(string LotId, decimal TotalSum);

public static class CalibreReweighter
{
    public const decimal DefaultTolerance = 0.03m;

    public static readonly string[] QualityCalibreColumns =
    [
        "Precalibre",
        "Calibre L",
        "Calibre XL",
        "Calibre J",
        "Calibre 2J",
        "Calibre 3J",
        "Calibre 4J",
        "Calibre 5J",
        "Calibre 6J"
    ];

    public static ReweightResult ReweightGroup(
        IReadOnlyList<decimal> reservedValues,
        IReadOnlyList<decimal> groupValues,
        int decimals = 2)
    {
        if (decimals < 0) decimals = 0;
        if (groupValues.Count == 0)
        {
            return new ReweightResult([], null);
        }

        decimal reservedSum = 0m;
        for (int i = 0; i < reservedValues.Count; i++)
        {
            reservedSum += reservedValues[i];
        }

        decimal groupSum = 0m;
        for (int i = 0; i < groupValues.Count; i++)
        {
            groupSum += groupValues[i];
        }

        if (reservedSum > 100m)
        {
            var unchanged = new decimal[groupValues.Count];
            for (int i = 0; i < groupValues.Count; i++)
            {
                unchanged[i] = Math.Round(groupValues[i], decimals, MidpointRounding.AwayFromZero);
            }
            return new ReweightResult(
                unchanged,
                $"La suma de columnas base ({reservedSum:0.##}%) excede 100%; se mantuvieron los valores originales sin reponderar.");
        }

        if (groupSum == 0m)
        {
            var zeros = new decimal[groupValues.Count];
            var warning = reservedSum < 100m
                ? "La suma del bloque a reponderar es 0%; no se puede distribuir el porcentaje restante."
                : null;
            return new ReweightResult(zeros, warning);
        }

        decimal factor = (100m - reservedSum) / 100m;
        var exact = new decimal[groupValues.Count];
        for (int i = 0; i < groupValues.Count; i++)
        {
            exact[i] = groupValues[i] * factor;
        }

        // Only apply largest-remainder closure when the source group actually sums to 100%.
        // If Firmpro sent a broken distribution (groupSum != 100), keep standard rounding so the
        // pre-generation tolerance check catches the real data anomaly.
        if (groupSum != 100m)
        {
            var standard = new decimal[groupValues.Count];
            for (int i = 0; i < groupValues.Count; i++)
            {
                standard[i] = Math.Round(exact[i], decimals, MidpointRounding.AwayFromZero);
            }
            return new ReweightResult(
                standard,
                $"La suma del bloque a reponderar es {groupSum:0.##}% (se esperaba 100%).");
        }

        decimal scale = Pow10(decimals);
        decimal targetBlockSum = Math.Round(100m - reservedSum, decimals, MidpointRounding.AwayFromZero);
        long targetUnits = (long)Math.Round(targetBlockSum * scale, 0, MidpointRounding.AwayFromZero);

        var units = new long[groupValues.Count];
        var remainders = new decimal[groupValues.Count];
        long currentUnits = 0;

        for (int i = 0; i < groupValues.Count; i++)
        {
            decimal scaled = exact[i] * scale;
            long floored = (long)Math.Floor(scaled);
            units[i] = floored;
            remainders[i] = scaled - floored;
            currentUnits += floored;
        }

        long diff = targetUnits - currentUnits;
        if (diff > 0)
        {
            var indices = Enumerable.Range(0, groupValues.Count)
                .OrderByDescending(i => remainders[i])
                .ThenBy(i => i)
                .ToArray();

            for (int k = 0; k < diff && k < indices.Length; k++)
            {
                units[indices[k]]++;
            }
        }
        else if (diff < 0)
        {
            var indices = Enumerable.Range(0, groupValues.Count)
                .OrderBy(i => remainders[i])
                .ThenByDescending(i => i)
                .ToArray();

            long toRemove = -diff;
            for (int k = 0; k < toRemove && k < indices.Length; k++)
            {
                units[indices[k]]--;
            }
        }

        var result = new decimal[groupValues.Count];
        for (int i = 0; i < groupValues.Count; i++)
        {
            result[i] = units[i] / scale;
        }

        return new ReweightResult(result, null);
    }

    public static IReadOnlyList<CalibreToleranceIssue> FindOutOfToleranceCalibres(
        IReadOnlyList<(string LotId, IReadOnlyDictionary<string, object?> TransformedRow)> rows,
        decimal tolerance = DefaultTolerance)
    {
        var issues = new List<CalibreToleranceIssue>();

        foreach (var (lotId, row) in rows)
        {
            bool hasAnyCalibreColumn = false;
            decimal total = 0m;

            foreach (var col in QualityCalibreColumns)
            {
                if (row.TryGetValue(col, out var rawVal))
                {
                    hasAnyCalibreColumn = true;
                    total += TryParseDecimal(rawVal);
                }
            }

            if (hasAnyCalibreColumn && Math.Abs(total - 100m) > tolerance)
            {
                issues.Add(new CalibreToleranceIssue(lotId, total));
            }
        }

        return issues;
    }

    public static string FormatToleranceAlertMessage(
        IReadOnlyList<CalibreToleranceIssue> issues,
        decimal tolerance = DefaultTolerance)
    {
        var tolStr = tolerance.ToString("0.00", CultureInfo.InvariantCulture);

        if (issues.Count == 1)
        {
            var issue = issues[0];
            var sumStr = issue.TotalSum.ToString("0.00", CultureInfo.InvariantCulture);
            return $"Para el lote {issue.LotId} la distribución de calibres no suma 100% ({sumStr}%) y está fuera del rango de tolerancia ±{tolStr}%.\n\n" +
                   "¿Desea continuar y generar el archivo de todas formas?\n" +
                   "Si cancela, los registros se mantendrán en estado \"Pendiente\".";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Los siguientes {issues.Count} lotes tienen una distribución de calibres que no suma 100% y están fuera del rango de tolerancia ±{tolStr}%:");
        sb.AppendLine();

        const int maxListed = 10;
        for (int i = 0; i < Math.Min(issues.Count, maxListed); i++)
        {
            var issue = issues[i];
            sb.AppendLine($"• Lote {issue.LotId}: {issue.TotalSum.ToString("0.00", CultureInfo.InvariantCulture)}%");
        }

        if (issues.Count > maxListed)
        {
            sb.AppendLine($"• ... y {issues.Count - maxListed} lote(s) más.");
        }

        sb.AppendLine();
        sb.AppendLine("¿Desea continuar y generar el archivo de todas formas?");
        sb.Append("Si cancela, los registros se mantendrán en estado \"Pendiente\".");
        return sb.ToString();
    }

    private static decimal Pow10(int exponent)
    {
        decimal result = 1m;
        for (int i = 0; i < exponent; i++)
        {
            result *= 10m;
        }
        return result;
    }

    private static decimal TryParseDecimal(object? val)
    {
        if (val is null || val == DBNull.Value) return 0m;

        switch (val)
        {
            case decimal dec: return dec;
            case double dbl when !double.IsNaN(dbl) && !double.IsInfinity(dbl): return (decimal)dbl;
            case float flt when !float.IsNaN(flt) && !float.IsInfinity(flt): return (decimal)flt;
            case int i: return i;
            case long l: return l;
            case short s: return s;
            case byte b: return b;
        }

        var str = val.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(str)) return 0m;
        if (!str.Contains('.') && str.Contains(',')) str = str.Replace(',', '.');

        return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;
    }
}
