using Homologador.AzureApi.Data;
using Homologador.AzureApi.Services;
using Homologador.Core.DTOs;
using Homologador.Core.Entities;
using Homologador.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Authorize(Policy = "HomologadorAdmin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AzureDbContext _azureDb;
    private readonly GrmRuleReader _grmRules;

    public AdminController(AzureDbContext azureDb, GrmRuleReader grmRules)
    {
        _azureDb = azureDb;
        _grmRules = grmRules;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _azureDb.Users
            .AsNoTracking()
            .Include(u => u.UserReports)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

        Dictionary<int, Report> reports;
        try
        {
            reports = (await _grmRules.GetConfigurationV4Async(
                Request.Headers.Authorization.ToString(), ct: ct)).Reports.ToDictionary(r => r.Id);
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }

        var dtos = users.Select(u =>
        {
            var reportsSummary = u.Rol == "Admin"
                ? "TODOS (Rol Admin)"
                : u.UserReports.Count == 0
                    ? "(Sin reportes asignados)"
                    : string.Join(", ", u.UserReports
                        .OrderBy(ur => ur.ReportId)
                        .Select(ur => reports.TryGetValue(ur.ReportId, out var rep)
                            ? $"[{rep.Codigo}] {rep.Name}"
                            : $"[Report #{ur.ReportId}]"));

            return new AdminUserDto(
                Id: u.Id,
                Codigo: u.Codigo,
                Email: u.Email,
                DisplayName: u.DisplayName,
                Rol: u.Rol,
                Perfil: u.Perfil,
                IsActive: u.IsActive,
                AssignedReportsSummary: reportsSummary,
                AssignedReportIds: u.UserReports.Select(ur => ur.ReportId).OrderBy(x => x).ToList()
            );
        }).ToList();

        return Ok(dtos);
    }

    [HttpPost("users/assign-reports")]
    public async Task<ActionResult> AssignUserReports([FromBody] AssignUserReportsRequest request, CancellationToken ct)
    {
        var existing = await _azureDb.UserReports.Where(ur => ur.UserId == request.UserId).ToListAsync(ct);
        _azureDb.UserReports.RemoveRange(existing);

        foreach (var reportId in request.ReportIds.Distinct())
        {
            _azureDb.UserReports.Add(new UserReport
            {
                UserId = request.UserId,
                ReportId = reportId
            });
        }

        await _azureDb.SaveChangesAsync(ct);
        return Ok(new { message = "Asignación de reportes actualizada." });
    }

    [HttpPost("users/reset-password")]
    public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await _azureDb.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, ct);
        if (user == null) return NotFound();

        var pwd = string.IsNullOrWhiteSpace(request.NewPassword) ? "admin123." : request.NewPassword.Trim();
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(pwd);

        await _azureDb.SaveChangesAsync(ct);
        return Ok(new { message = $"Contraseña restablecida para {user.Email}." });
    }

    [HttpGet("reports")]
    public async Task<ActionResult<IReadOnlyList<ReportSummaryDto>>> GetReports(CancellationToken ct)
    {
        try
        {
            return Ok(await _grmRules.GetReportSummariesV4Async(
                Request.Headers.Authorization.ToString(), administrative: true, ct: ct));
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("pipelines")]
    public async Task<ActionResult<IReadOnlyList<AdminPipelineDto>>> GetPipelines(CancellationToken ct)
    {
        var result = new List<AdminPipelineDto>();
        foreach (var process in ReportProcessMap.All.OrderBy(p => p.MappingPipelineId))
        {
            IReadOnlyList<PipelineRuleDto> rules;
            try
            {
                rules = await _grmRules.GetRulesAsync(process.ReportId,
                    HttpContext.Request.Headers.Authorization.ToString(), ct);
            }
            catch (GrmRuleFetchException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
            result.Add(new AdminPipelineDto(
                process.MappingPipelineId, process.Codigo, process.Name,
                process.Description, true, rules.Count, rules));
        }
        return Ok(result);
    }

    [HttpGet("lookups")]
    public async Task<ActionResult<IReadOnlyList<LookupTableDto>>> GetLookups(CancellationToken ct)
    {
        try
        {
            return Ok(await _grmRules.GetLookupsAsync(Request.Headers.Authorization.ToString(), ct));
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("datasources")]
    public async Task<ActionResult<IReadOnlyList<AdminDataSourceDto>>> GetDataSources(CancellationToken ct)
    {
        try
        {
            var configuration = await _grmRules.GetConfigurationV4Async(
                Request.Headers.Authorization.ToString(), ct: ct);
            return Ok(GrmConfigurationMapper.ToAdminDataSources(configuration));
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("queries")]
    public async Task<ActionResult<IReadOnlyList<AdminQueryDto>>> GetQueries(CancellationToken ct)
    {
        try
        {
            var configuration = await _grmRules.GetConfigurationV4Async(
                Request.Headers.Authorization.ToString(), ct: ct);
            return Ok(GrmConfigurationMapper.ToAdminQueries(configuration));
        }
        catch (GrmRuleFetchException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

}
