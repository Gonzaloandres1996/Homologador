namespace Homologador.Core.Services;

public readonly record struct ReportProcess(
    int ReportId, int MappingPipelineId, int TipoProceso,
    string Codigo, string Name, string Description);

public static class ReportProcessMap
{
    public static IReadOnlyList<ReportProcess> All { get; } =
    [
        new(1, 1, 2, "0001", "Calidad Andinexia", "Carga de calidad Andinexia"),
        new(2, 3, 1, "0003", "Recepciones 26-10", "Mapeo hacia formato SAP con columnas reales de DAIA"),
        new(3, 2, 3, "0002", "Pipeline SAP Producción", "Mapeo hacia formato SAP con columnas reales de DAIA")
    ];

    public static ReportProcess ForReport(int reportId) => reportId switch
    {
        1 => All[0],
        2 => All[1],
        3 => All[2],
        _ => throw new InvalidOperationException($"El reporte {reportId} no tiene proceso V3 configurado.")
    };

    public static ReportProcess ForPipeline(int pipelineId) => pipelineId switch
    {
        1 => All[0],
        2 => All[2],
        3 => All[1],
        _ => throw new InvalidOperationException($"El pipeline {pipelineId} no tiene proceso V3 configurado.")
    };
}
