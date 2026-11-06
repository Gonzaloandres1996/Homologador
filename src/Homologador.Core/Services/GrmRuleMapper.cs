using System.Globalization;
using Homologador.Core.DTOs;

namespace Homologador.Core.Services;

// GET /api/conversor/reglas?tipoProceso={tipoProceso}
public sealed record GrmRulesResponse(int? TipoProceso, IReadOnlyList<GrmRuleRow>? Reglas);

public sealed record GrmRuleRow
{
    public int? IdEquivalencia { get; init; }
    public int? IdAplicacion { get; init; }
    public int? IdParametro { get; init; }
    public string? Parametro { get; init; }
    public string? Valor { get; init; }
    public int? TipoProceso { get; init; }
    public string? Valor1 { get; init; }
    public string? Valor2 { get; init; }
    public string? Valor3 { get; init; }
    public string? Valor4 { get; init; }
    public string? Valor5 { get; init; }
    public string? Valor6 { get; init; }
    public string? Valor7 { get; init; }
    public string? Valor8 { get; init; }
    public string? Valor9 { get; init; }
    public string? Valor10 { get; init; }
    public string? Valor11 { get; init; }
    public string? Valor12 { get; init; }
    public string? Valor13 { get; init; }
    public string? Valor14 { get; init; }
}

public static class GrmRuleMapper
{
    public static ExecutionContextDto WithRules(ExecutionContextDto context, GrmRulesResponse response)
    {
        var rules = MapRules(context.Report.Id, response);
        return context with
        {
            Report = context.Report with { RuleCount = rules.Count },
            Rules = rules,
            Lookups = []
        };
    }

    public static IReadOnlyList<PipelineRuleDto> MapRules(int reportId, GrmRulesResponse response)
    {
        var process = ReportProcessMap.ForReport(reportId);
        if (response.TipoProceso != process.TipoProceso || response.Reglas is null
            || response.Reglas.Count == 0)
            throw new InvalidOperationException("GRM devolvio reglas incompletas o de otro proceso.");

        var ids = new HashSet<int>();
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rules = new List<PipelineRuleDto>(response.Reglas.Count);
        foreach (var row in response.Reglas)
        {
            if (row is null || row.IdEquivalencia is not > 0 || !ids.Add(row.IdEquivalencia.Value)
                || row.IdAplicacion != 2 || row.IdParametro is not null
                || row.Parametro is not null || row.Valor is not null
                || row.TipoProceso != process.TipoProceso || row.Valor2 is null
                || !targets.Add(row.Valor2) || string.IsNullOrWhiteSpace(row.Valor3))
                throw new InvalidOperationException("GRM devolvio una regla V3 invalida.");

            var sortOrder = RequiredInt(row.Valor1, nameof(row.Valor1));
            var lookupId = OptionalInt(row.Valor6, nameof(row.Valor6));
            var start = OptionalInt(row.Valor13, nameof(row.Valor13));
            var length = OptionalInt(row.Valor14, nameof(row.Valor14));
            rules.Add(new PipelineRuleDto(
                row.IdEquivalencia.Value,
                sortOrder,
                row.Valor2,
                row.Valor3,
                row.Valor4,
                row.Valor5,
                lookupId,
                row.Valor7,
                row.Valor8,
                row.Valor9,
                row.Valor10,
                RequiredBit(row.Valor11, nameof(row.Valor11)),
                RequiredBit(row.Valor12, nameof(row.Valor12)),
                start,
                length));
        }

        rules.Sort((left, right) => left.SortOrder.CompareTo(right.SortOrder));
        for (var index = 0; index < rules.Count; index++)
        {
            if (rules[index].SortOrder != index + 1)
                throw new InvalidOperationException("GRM devolvio un orden de reglas incompleto o duplicado.");
        }

        return rules;
    }

    private static int RequiredInt(string? value, string field)
    {
        if (value is null || !int.TryParse(value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var parsed))
            throw new InvalidOperationException($"GRM devolvio {field} invalido.");
        return parsed;
    }

    private static int? OptionalInt(string? value, string field) =>
        value is null ? null : RequiredInt(value, field);

    private static bool RequiredBit(string? value, string field) => value switch
    {
        "0" => false,
        "1" => true,
        _ => throw new InvalidOperationException($"GRM devolvio {field} invalido.")
    };
}
