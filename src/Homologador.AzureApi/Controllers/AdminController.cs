using Homologador.AzureApi.Data;
using Homologador.Core.DTOs;
using Homologador.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly AzureDbContext _azureDb;
    private readonly ConfigDbContext _configDb;

    public AdminController(AzureDbContext azureDb, ConfigDbContext configDb)
    {
        _azureDb = azureDb;
        _configDb = configDb;
    }

    [HttpGet("users")]
    public async Task<ActionResult<IReadOnlyList<AdminUserDto>>> GetUsers(CancellationToken ct)
    {
        var users = await _azureDb.Users
            .AsNoTracking()
            .Include(u => u.UserReports)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

        var reports = await _configDb.Reports
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Id, ct);

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
        var reports = await _configDb.Reports
            .AsNoTracking()
            .Include(r => r.QueryDefinition)
            .Include(r => r.MappingPipeline)
                .ThenInclude(p => p.Rules)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        return Ok(reports.Select(r => new ReportSummaryDto(
            r.Id,
            r.Codigo,
            r.Name,
            r.Description,
            r.TargetArea,
            $"[{r.QueryDefinition.Codigo}] {r.QueryDefinition.Name}",
            $"[{r.MappingPipeline.Codigo}] {r.MappingPipeline.Name}",
            r.MappingPipeline.Rules.Count,
            r.OutputFormat,
            r.OutputSheetName,
            r.OutputFilePattern
        )).ToList());
    }

    [HttpGet("pipelines")]
    public async Task<ActionResult<IReadOnlyList<AdminPipelineDto>>> GetPipelines(CancellationToken ct)
    {
        var pipelines = await _configDb.MappingPipelines
            .AsNoTracking()
            .Include(p => p.Rules)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        var dtos = pipelines.Select(p =>
        {
            var rules = p.Rules.OrderBy(r => r.SortOrder).Select(r => new PipelineRuleDto(
                r.Id,
                r.SortOrder,
                r.TargetColumn,
                r.RuleType,
                r.SourceColumn,
                r.FixedValue,
                r.LookupTableId,
                r.LookupKeyColumn,
                r.LookupValueColumn,
                r.DateFormat,
                r.Validation,
                r.IsRequired,
                r.IsIdentity,
                r.SubstringStart,
                r.SubstringLength
            )).ToList();

            return new AdminPipelineDto(
                p.Id,
                p.Codigo,
                p.Name,
                p.Description,
                p.IsActive,
                rules.Count,
                rules
            );
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("lookups")]
    public async Task<ActionResult<IReadOnlyList<LookupTableDto>>> GetLookups(CancellationToken ct)
    {
        var tables = await _configDb.LookupTables
            .AsNoTracking()
            .Include(t => t.Entries)
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        return Ok(tables.Select(t => new LookupTableDto(
            t.Id,
            t.Codigo,
            t.Name,
            t.Description,
            t.Entries.OrderBy(e => e.Id).Select(e => new LookupEntryDto(e.Id, e.KeyValue, e.MappedValue, e.ExtraField1)).ToList()
        )).ToList());
    }

    [HttpGet("datasources")]
    public async Task<ActionResult<IReadOnlyList<AdminDataSourceDto>>> GetDataSources(CancellationToken ct)
    {
        var list = await _configDb.DataSources.AsNoTracking().OrderBy(d => d.Id).ToListAsync(ct);
        return Ok(list.Select(d => new AdminDataSourceDto(
            d.Id,
            d.Codigo,
            d.Name,
            d.Server,
            d.Database,
            d.AuthType == 1 ? "SQL Authentication" : "Windows Integrated",
            d.ConnectionTimeout,
            d.IsActive
        )).ToList());
    }

    [HttpGet("queries")]
    public async Task<ActionResult<IReadOnlyList<AdminQueryDto>>> GetQueries(CancellationToken ct)
    {
        var list = await _configDb.QueryDefinitions
            .AsNoTracking()
            .Include(q => q.DataSource)
            .Include(q => q.Parameters)
            .OrderBy(q => q.Id)
            .ToListAsync(ct);

        return Ok(list.Select(q =>
        {
            var prms = q.Parameters.OrderBy(p => p.SortOrder).Select(p => new QueryParamDto(
                p.Id,
                p.SortOrder,
                p.Name,
                p.SqlType,
                p.DefaultValue,
                p.Description
            )).ToList();

            return new AdminQueryDto(
                q.Id,
                q.Codigo,
                q.Name,
                q.DataSource.Codigo,
                q.DataSource.Name,
                q.CommandType == 1 ? "StoredProcedure" : "View / SELECT",
                q.CommandText,
                prms.Count,
                prms
            );
        }).ToList());
    }
}
