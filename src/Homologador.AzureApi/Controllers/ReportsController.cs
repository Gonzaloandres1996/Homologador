using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Homologador.AzureApi.Data;
using Homologador.Core.DTOs;
using Homologador.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly AzureDbContext _azureDb;
    private readonly ConfigDbContext _configDb;
    private readonly IEncryptionService _encryption;

    public ReportsController(AzureDbContext azureDb, ConfigDbContext configDb, IEncryptionService encryption)
    {
        _azureDb = azureDb;
        _configDb = configDb;
        _encryption = encryption;
    }

    [HttpGet("my-reports")]
    public async Task<ActionResult<IReadOnlyList<ReportSummaryDto>>> GetMyReports(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        var query = _configDb.Reports
            .AsNoTracking()
            .Include(r => r.QueryDefinition)
            .Include(r => r.MappingPipeline)
                .ThenInclude(p => p.Rules)
            .Where(r => r.IsActive);

        if (!isAdmin)
        {
            var assignedIds = await _azureDb.UserReports
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.ReportId)
                .ToListAsync(ct);

            query = query.Where(r => assignedIds.Contains(r.Id));
        }

        var reports = await query.OrderBy(r => r.Id).ToListAsync(ct);

        var result = reports.Select(r => new ReportSummaryDto(
            Id: r.Id,
            Codigo: r.Codigo,
            Name: r.Name,
            Description: r.Description,
            TargetArea: r.TargetArea,
            QueryName: r.QueryDefinition.Name,
            PipelineName: r.MappingPipeline.Name,
            RuleCount: r.MappingPipeline.Rules.Count,
            OutputFormat: r.OutputFormat,
            OutputSheetName: r.OutputSheetName,
            OutputFilePattern: r.OutputFilePattern
        )).ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}/execution-context")]
    public async Task<ActionResult<ExecutionContextDto>> GetExecutionContext(int id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        if (!isAdmin)
        {
            var hasAccess = await _azureDb.UserReports
                .AsNoTracking()
                .AnyAsync(ur => ur.UserId == userId && ur.ReportId == id, ct);

            if (!hasAccess)
            {
                return Forbid();
            }
        }

        var report = await _configDb.Reports
            .AsNoTracking()
            .Include(r => r.QueryDefinition)
                .ThenInclude(q => q.DataSource)
            .Include(r => r.QueryDefinition)
                .ThenInclude(q => q.Parameters)
            .Include(r => r.MappingPipeline)
                .ThenInclude(p => p.Rules)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (report == null)
        {
            return NotFound(new { message = "Reporte no encontrado." });
        }

        var ds = report.QueryDefinition.DataSource;
        string? decUser = null;
        string? decPass = null;
        if (!string.IsNullOrEmpty(ds.UsernameEncrypted))
        {
            try { decUser = _encryption.Decrypt(ds.UsernameEncrypted); } catch { decUser = ds.UsernameEncrypted; }
        }
        if (!string.IsNullOrEmpty(ds.PasswordEncrypted))
        {
            try { decPass = _encryption.Decrypt(ds.PasswordEncrypted); } catch { decPass = ds.PasswordEncrypted; }
        }

        var lookupIds = report.MappingPipeline.Rules
            .Where(r => r.LookupTableId.HasValue)
            .Select(r => r.LookupTableId!.Value)
            .Distinct()
            .ToList();

        var lookups = await _configDb.LookupTables
            .AsNoTracking()
            .Include(t => t.Entries)
            .Where(t => lookupIds.Contains(t.Id))
            .OrderBy(t => t.Id)
            .ToListAsync(ct);

        List<string> alreadyGeneratedKeys;
        if (report.TargetArea.StartsWith("Producc", StringComparison.OrdinalIgnoreCase))
        {
            alreadyGeneratedKeys = await _azureDb.RegistrosProduccion
                .AsNoTracking()
                .Where(x => x.ReportId == id && (x.Estado == "Cargado" || x.Estado == "Generado"))
                .Select(x => x.LoteProceso + "|" + x.CodigoPallet)
                .Distinct()
                .ToListAsync(ct);
        }
        else if (report.TargetArea.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase))
        {
            alreadyGeneratedKeys = await _azureDb.RegistrosCalidad
                .AsNoTracking()
                .Where(x => x.Estado == "Cargado" || x.Estado == "Generado")
                .Select(x => x.Lote)
                .ToListAsync(ct);
        }
        else
        {
            alreadyGeneratedKeys = await _azureDb.RegistrosRecepcion
                .AsNoTracking()
                .Where(x => x.Estado == "Cargado" || x.Estado == "Generado")
                .Select(x => x.Lote)
                .ToListAsync(ct);
        }

        // Deduplicate query parameters by Name (case-insensitive) while preserving SortOrder
        var distinctParams = report.QueryDefinition.Parameters
            .OrderBy(p => p.SortOrder)
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(p => new QueryParamDto(
                p.Id,
                p.SortOrder,
                p.Name,
                p.SqlType,
                p.DefaultValue,
                p.Description
            ))
            .ToList();

        var orderedRules = report.MappingPipeline.Rules
            .OrderBy(r => r.SortOrder)
            .Select(r => new PipelineRuleDto(
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
            ))
            .ToList();

        var lookupDtos = lookups.Select(t => new LookupTableDto(
            t.Id,
            t.Codigo,
            t.Name,
            t.Description,
            t.Entries
                .OrderBy(e => e.KeyValue)
                .Select(e => new LookupEntryDto(e.Id, e.KeyValue, e.MappedValue, e.ExtraField1))
                .ToList()
        )).ToList();

        var dto = new ExecutionContextDto(
            Report: new ReportSummaryDto(
                report.Id,
                report.Codigo,
                report.Name,
                report.Description,
                report.TargetArea,
                report.QueryDefinition.Name,
                report.MappingPipeline.Name,
                orderedRules.Count,
                report.OutputFormat,
                report.OutputSheetName,
                report.OutputFilePattern
            ),
            DataSource: new DataSourceConnectDto(
                ds.Id,
                ds.Codigo,
                ds.Name,
                ds.Server,
                ds.Database,
                ds.AuthType,
                decUser,
                decPass,
                ds.ConnectionTimeout
            ),
            Query: new QueryExecutionDto(
                report.QueryDefinition.Id,
                report.QueryDefinition.Codigo,
                report.QueryDefinition.Name,
                report.QueryDefinition.CommandType,
                report.QueryDefinition.CommandText,
                distinctParams
            ),
            Rules: orderedRules,
            Lookups: lookupDtos,
            AlreadyGeneratedKeys: alreadyGeneratedKeys
        );

        return Ok(dto);
    }

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(raw, out var id) ? id : 0;
    }
}
