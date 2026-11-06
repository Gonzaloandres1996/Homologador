using Homologador.AzureApi.Security;
using Homologador.AzureApi.Services;
using Homologador.Core.DTOs;
using Homologador.Core.Entities;
using Homologador.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Authorize]
[Route("api/records")]
public class RecordsController(GrmRuleReader grmRules, GrmProfileAccess access) : ControllerBase
{
    private async Task<Report> GetReport(int reportId, CancellationToken ct)
    {
        ReportProcess process;
        try { process = ReportProcessMap.ForReport(reportId); }
        catch (InvalidOperationException) { throw new GrmRuleFetchException(404, "Reporte no encontrado."); }
        var configuration = await grmRules.GetConfigurationV4Async(
            Request.Headers.Authorization.ToString(), process.TipoProceso, ct);
        return configuration.Reports.Single(r => r.Id == reportId);
    }

    [HttpPost("mark-generated")]
    public async Task<ActionResult<MarkGeneratedResponse>> MarkGenerated(
        [FromBody] MarkGeneratedRequest request, CancellationToken ct)
    {
        var identity = await access.GetAsync(HttpContext, ct);
        if (identity.User is null) return StatusCode(identity.StatusCode);
        try
        {
            var report = await GetReport(request.ReportId, ct);
            if (!report.IsActive || !identity.HasArea(report.TargetArea)
                || !string.Equals(request.TargetArea, report.TargetArea, StringComparison.Ordinal))
                return Forbid();
            return StatusCode(410, new { message = "La persistencia de los reportes se registra en GRM." });
        }
        catch (GrmRuleFetchException ex)
        {
            if (ex.StatusCode == 404) return NotFound();
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("by-report/{reportId:int}")]
    public async Task<ActionResult<IReadOnlyList<RecordGridRowDto>>> GetByReport(
        int reportId, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var identity = await access.GetAsync(HttpContext, ct);
        if (identity.User is null) return StatusCode(identity.StatusCode);
        try
        {
            var report = await GetReport(reportId, ct);
            if (!report.IsActive || !identity.HasArea(report.TargetArea)) return Forbid();
            return StatusCode(410, new { message = "La persistencia de los reportes se consulta en GRM." });
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("{area}")]
    public async Task<ActionResult<IReadOnlyList<RecordGridRowDto>>> GetByArea(
        string area, [FromQuery] string? search = null,
        [FromQuery] int? reportId = null, CancellationToken ct = default)
    {
        var identity = await access.GetAsync(HttpContext, ct);
        if (identity.User is null) return StatusCode(identity.StatusCode);
        var canonicalArea = area.StartsWith("Producc", StringComparison.OrdinalIgnoreCase)
            ? "Producción" : area.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase)
                ? "Calidad" : area.StartsWith("Recepc", StringComparison.OrdinalIgnoreCase)
                    ? "Recepción" : string.Empty;
        if (!identity.HasArea(canonicalArea)) return Forbid();
        try
        {
            var configuration = await grmRules.GetConfigurationV4Async(
                Request.Headers.Authorization.ToString(), ct: ct);
            if (reportId.HasValue && !configuration.Reports.Any(r =>
                r.Id == reportId && r.IsActive && r.TargetArea == canonicalArea)) return Forbid();
            return StatusCode(410, new { message = "La persistencia de los reportes se consulta en GRM." });
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
