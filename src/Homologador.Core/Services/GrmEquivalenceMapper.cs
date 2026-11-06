using Homologador.Core.DTOs;

namespace Homologador.Core.Services;

public sealed record GrmEquivalenceRow(int IdParametro, string Parametro, string Valor);

public static class GrmEquivalenceMapper
{
    private static readonly (int LookupId, int ParameterId, string Name)[] Groups =
    [
        (1, 2, "Especies"),
        (2, 3, "Esponjas"),
        (3, 4, "Polvo Tierra"),
        (4, 5, "Tratamiento")
    ];

    public static ExecutionContextDto WithLookups(
        ExecutionContextDto context, IReadOnlyList<GrmEquivalenceRow> equivalences)
    {
        var required = context.Rules.Where(rule => rule.LookupTableId.HasValue)
            .Select(rule => rule.LookupTableId!.Value).Distinct().ToArray();
        return context with { Lookups = ToLookupTables(equivalences, required) };
    }

    public static IReadOnlyList<LookupTableDto> ToLookupTables(
        IReadOnlyList<GrmEquivalenceRow> equivalences, IReadOnlyList<int>? required = null)
    {
        required ??= Groups.Select(g => g.LookupId).ToArray();
        var lookups = new List<LookupTableDto>();
        foreach (var lookupId in required)
        {
            var group = Groups.FirstOrDefault(item => item.LookupId == lookupId);
            if (group.LookupId == 0)
                throw new InvalidOperationException($"Lookup {lookupId} no tiene correspondencia GRM.");

            var rows = equivalences.Where(row => row.IdParametro == group.ParameterId).ToArray();
            if (rows.Length == 0)
                throw new InvalidOperationException($"GRM no devolvio el grupo requerido {group.Name}.");

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var entries = new List<LookupEntryDto>(rows.Length);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Parametro)
                    || string.IsNullOrWhiteSpace(row.Valor)
                    || !keys.Add(row.Parametro.Trim()))
                    throw new InvalidOperationException($"GRM devolvio equivalencias invalidas para {group.Name}.");
                entries.Add(new LookupEntryDto(entries.Count + 1, row.Parametro, row.Valor, null));
            }
            lookups.Add(new LookupTableDto(lookupId, group.Name, group.Name, null, entries));
        }
        return lookups;
    }
}
