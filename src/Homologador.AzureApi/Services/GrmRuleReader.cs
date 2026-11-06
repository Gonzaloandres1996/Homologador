using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Homologador.Core.DTOs;
using Homologador.Core.Services;

namespace Homologador.AzureApi.Services;

public sealed class GrmRuleFetchException(int statusCode, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class GrmRuleReader(HttpClient http)
{
    public async Task<IReadOnlyList<PipelineRuleDto>> GetRulesAsync(
        int reportId, string authorization, CancellationToken ct)
    {
        var header = Authorization(authorization);
        var process = ReportProcessMap.ForReport(reportId);
        var body = await ReadAsync<GrmRulesResponse>(
            $"api/conversor/reglas?tipoProceso={process.TipoProceso}", header, "reglas V3", ct, true);
        try
        {
            return GrmRuleMapper.MapRules(reportId, body);
        }
        catch (InvalidOperationException ex)
        {
            throw new GrmRuleFetchException(502, "GRM devolvio reglas V3 incompletas o invalidas.", ex);
        }
    }

    // Configuration uses the published GRM V4 contract; never fall back to local SQL.
    public async Task<GrmConfigurationSet> GetConfigurationV4Async(
        string authorization, int? tipoProceso = null, CancellationToken ct = default)
    {
        var header = Authorization(authorization);
        if (tipoProceso.HasValue && tipoProceso is < 1 or > 3)
            throw new GrmRuleFetchException(400, "TipoProceso V4 debe ser 1, 2 o 3.");
        var path = "api/conversor/configuracion"
            + (tipoProceso.HasValue ? $"?tipoProceso={tipoProceso.Value}" : "");
        var body = await ReadAsync<GrmConfigurationResponse>(path, header, "configuraciones V4", ct);
        try
        {
            return GrmConfigurationMapper.Map(body, tipoProceso);
        }
        catch (InvalidOperationException)
        {
            throw new GrmRuleFetchException(502, "GRM devolvio configuraciones V4 incompletas o invalidas.");
        }
    }

    public async Task<ExecutionContextDto> GetExecutionContextV4Async(int reportId,
        string authorization, IEncryptionService encryption, CancellationToken ct = default,
        IReadOnlyCollection<string>? allowedAreas = null)
    {
        Authorization(authorization);
        ReportProcess process;
        try { process = ReportProcessMap.ForReport(reportId); }
        catch (InvalidOperationException) { throw new GrmRuleFetchException(404, "Reporte no encontrado."); }
        var configuration = await GetConfigurationV4Async(authorization, process.TipoProceso, ct);
        var report = configuration.Reports.Single(r => r.Id == reportId);
        if (!report.IsActive || (allowedAreas is not null && !allowedAreas.Contains(report.TargetArea)))
            throw new GrmRuleFetchException(403, "Reporte no disponible.");
        var rules = await GetRulesAsync(reportId, authorization, ct);
        try
        {
            // V2 lookups are still completed by HomologadorApiClient exactly as today.
            return GrmConfigurationMapper.ToExecutionContext(report, rules, encryption);
        }
        catch (InvalidOperationException)
        {
            throw new GrmRuleFetchException(502, "No se pudo reconstruir el contexto V4 o descifrar sus credenciales.");
        }
    }

    public async Task<IReadOnlyList<ReportSummaryDto>> GetReportSummariesV4Async(
        string authorization, bool administrative = false, CancellationToken ct = default,
        IReadOnlyCollection<string>? allowedAreas = null)
    {
        var configuration = await GetConfigurationV4Async(authorization, ct: ct);
        var result = new List<ReportSummaryDto>();
        foreach (var report in configuration.Reports.Where(r => (administrative || r.IsActive)
            && (allowedAreas is null || allowedAreas.Contains(r.TargetArea))))
        {
            var rules = await GetRulesAsync(report.Id, authorization, ct);
            result.Add(GrmConfigurationMapper.ToReportSummary(report, rules.Count, administrative));
        }
        return result;
    }

    public async Task<IReadOnlyList<LookupTableDto>> GetLookupsAsync(
        string authorization, CancellationToken ct)
    {
        var rows = await ReadAsync<List<GrmEquivalenceRow>>("api/conversor/equivalencias",
            Authorization(authorization), "equivalencias V2", ct);
        try { return GrmEquivalenceMapper.ToLookupTables(rows); }
        catch (InvalidOperationException)
        {
            throw new GrmRuleFetchException(502, "GRM devolvio equivalencias V2 incompletas o invalidas.");
        }
    }

    private static AuthenticationHeaderValue Authorization(string authorization)
    {
        if (!AuthenticationHeaderValue.TryParse(authorization, out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter))
            throw new GrmRuleFetchException(401, "Debe iniciar sesion en GRM.");
        return header;
    }

    private async Task<T> ReadAsync<T>(string path, AuthenticationHeaderValue header, string noun,
        CancellationToken ct, bool includeInnerErrors = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = header;

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var status = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => 401,
                    HttpStatusCode.Forbidden => 403,
                    _ => 503
                };
                throw new GrmRuleFetchException(status,
                    $"GRM no devolvio las {noun} (HTTP {(int)response.StatusCode}).");
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
                    ?? throw new GrmRuleFetchException(502, $"GRM devolvio {noun} incompletas o invalidas.");
            }
            catch (JsonException ex)
            {
                throw new GrmRuleFetchException(502, $"GRM devolvio {noun} invalidas.", includeInnerErrors ? ex : null);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new GrmRuleFetchException(503, $"No se pudo conectar con las {noun} de GRM.", includeInnerErrors ? ex : null);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new GrmRuleFetchException(503, $"Se agoto el tiempo de espera de las {noun} de GRM.", includeInnerErrors ? ex : null);
        }
    }
}
