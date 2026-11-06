using Homologador.AzureApi.Security;
using Homologador.AzureApi.Services;
using Homologador.Core.DTOs;
using Homologador.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController(
    IEncryptionService encryption, GrmProfileAccess access, GrmRuleReader grmRules) : ControllerBase
{
    private static string[] AllowedAreas(GrmAccessResult identity) =>
        new[] { "Recepción", "Calidad", "Producción" }.Where(identity.HasArea).ToArray();

    [HttpGet("my-reports")]
    public async Task<ActionResult<IReadOnlyList<ReportSummaryDto>>> GetMyReports(CancellationToken ct)
    {
        var identity = await access.GetAsync(HttpContext, ct);
        if (identity.User is null) return StatusCode(identity.StatusCode);
        var allowedAreas = AllowedAreas(identity);
        if (allowedAreas.Length == 0) return Ok(Array.Empty<ReportSummaryDto>());
        try
        {
            return Ok(await grmRules.GetReportSummariesV4Async(
                Request.Headers.Authorization.ToString(), ct: ct, allowedAreas: allowedAreas));
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}/execution-context")]
    public async Task<ActionResult<ExecutionContextDto>> GetExecutionContext(int id, CancellationToken ct)
    {
        var identity = await access.GetAsync(HttpContext, ct);
        if (identity.User is null) return StatusCode(identity.StatusCode);
        try
        {
            return Ok(await grmRules.GetExecutionContextV4Async(id,
                Request.Headers.Authorization.ToString(), encryption, ct, AllowedAreas(identity)));
        }
        catch (GrmRuleFetchException ex)
        {
            if (ex.StatusCode == 403) return Forbid();
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
