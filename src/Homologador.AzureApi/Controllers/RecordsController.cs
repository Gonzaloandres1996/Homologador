using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Homologador.AzureApi.Data;
using Homologador.Core.DTOs;
using Homologador.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Authorize]
[Route("api/records")]
public class RecordsController : ControllerBase
{
    private readonly MigracionDbContext _db;

    public RecordsController(MigracionDbContext db)
    {
        _db = db;
    }

    [HttpPost("mark-generated")]
    public async Task<ActionResult<MarkGeneratedResponse>> MarkGenerated([FromBody] MarkGeneratedRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");
        var now = request.FechaHoraCreacionArchivo == default ? DateTime.UtcNow : request.FechaHoraCreacionArchivo;

        if (isAdmin)
        {
            // Admin is strictly for test execution and must never alter statuses or persistence.
            return Ok(new MarkGeneratedResponse(
                InsertedCount: 0,
                SkippedDuplicates: request.GeneratedRows.Count,
                Area: request.TargetArea,
                SavedAt: DateTime.UtcNow
            ));
        }

        int inserted = 0;
        int skipped = 0;

        if (request.TargetArea.StartsWith("Producc", StringComparison.OrdinalIgnoreCase))
        {
            var existingKeys = (await _db.RegistrosProduccion
                .AsNoTracking()
                .Where(x => x.ReportId == request.ReportId)
                .Select(x => x.LoteProceso + "|" + x.CodigoPallet)
                .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in request.GeneratedRows)
            {
                var key = row.PrimaryKey?.Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (!existingKeys.Add(key))
                {
                    skipped++;
                    continue;
                }

                string loteProceso;
                string codigoPallet;
                var parts = key.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2)
                {
                    loteProceso = parts[0];
                    codigoPallet = parts[1];
                }
                else
                {
                    loteProceso = parts[0];
                    codigoPallet = row.Calibre ?? parts[0];
                }

                _db.RegistrosProduccion.Add(new RegistroProduccion
                {
                    LoteProceso = loteProceso,
                    CodigoPallet = codigoPallet,
                    FechaHoraCreacionArchivo = now,
                    Productor = row.Productor,
                    Calibre = row.Calibre,
                    Estado = "Cargado",
                    ReportId = request.ReportId
                });
                inserted++;
            }
        }
        else if (request.TargetArea.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase))
        {
            var existingLots = (await _db.RegistrosCalidad
                .AsNoTracking()
                .Select(x => x.Lote)
                .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var recepcionProductorByLote = await _db.RegistrosRecepcion
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Lote, x => x.Productor, StringComparer.OrdinalIgnoreCase, ct);

            foreach (var row in request.GeneratedRows)
            {
                var key = row.PrimaryKey?.Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (!existingLots.Add(key))
                {
                    skipped++;
                    continue;
                }

                var productor = row.Productor;
                if (productor == null && recepcionProductorByLote.TryGetValue(key, out var recProd))
                {
                    productor = recProd;
                }

                _db.RegistrosCalidad.Add(new RegistroCalidad
                {
                    Lote = key,
                    FechaHoraCreacionArchivo = now,
                    Productor = productor,
                    Estado = "Cargado",
                    ReportId = request.ReportId
                });
                inserted++;
            }
        }
        else // Recepción
        {
            var existingLots = (await _db.RegistrosRecepcion
                .AsNoTracking()
                .Select(x => x.Lote)
                .ToListAsync(ct))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var row in request.GeneratedRows)
            {
                var key = row.PrimaryKey?.Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (!existingLots.Add(key))
                {
                    skipped++;
                    continue;
                }

                _db.RegistrosRecepcion.Add(new RegistroRecepcion
                {
                    Lote = key,
                    FechaHoraCreacionArchivo = now,
                    Productor = row.Productor,
                    Kg = row.Kg,
                    Estado = "Cargado",
                    ReportId = request.ReportId
                });
                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new MarkGeneratedResponse(
            InsertedCount: inserted,
            SkippedDuplicates: skipped,
            Area: request.TargetArea,
            SavedAt: now
        ));
    }

    [HttpGet("by-report/{reportId:int}")]
    public async Task<ActionResult<IReadOnlyList<RecordGridRowDto>>> GetByReport(
        int reportId,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var report = await _db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reportId, ct);
        if (report == null)
        {
            return NotFound(new { message = "Reporte no encontrado." });
        }

        return await GetByArea(report.TargetArea, search, reportId, ct);
    }

    [HttpGet("{area}")]
    public async Task<ActionResult<IReadOnlyList<RecordGridRowDto>>> GetByArea(
        string area,
        [FromQuery] string? search = null,
        [FromQuery] int? reportId = null,
        CancellationToken ct = default)
    {
        var term = search?.Trim();
        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("Admin");

        List<int>? allowedReportIds = null;
        if (!isAdmin)
        {
            allowedReportIds = await _db.UserReports
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.ReportId)
                .ToListAsync(ct);
        }

        if (area.StartsWith("Producc", StringComparison.OrdinalIgnoreCase))
        {
            var q = _db.RegistrosProduccion
                .AsNoTracking()
                .Include(x => x.Report)
                .AsQueryable();

            if (allowedReportIds != null)
            {
                q = q.Where(x => allowedReportIds.Contains(x.ReportId));
            }

            if (reportId.HasValue)
            {
                q = q.Where(x => x.ReportId == reportId.Value);
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x => x.LoteProceso.Contains(term) ||
                                 x.CodigoPallet.Contains(term) ||
                                 (x.Productor != null && x.Productor.Contains(term)) ||
                                 (x.Calibre != null && x.Calibre.Contains(term)));
            }

            var list = await q
                .OrderByDescending(x => x.FechaHoraCreacionArchivo)
                .ThenBy(x => x.LoteProceso)
                .ThenBy(x => x.CodigoPallet)
                .Take(1000)
                .ToListAsync(ct);

            return Ok(list.Select(x => new RecordGridRowDto(
                CodigoOrLote: x.CodigoPallet,
                LoteOrProceso: x.LoteProceso,
                Productor: x.Productor,
                Kg: null,
                Calibre: x.Calibre,
                Estado: "Cargado",
                FechaHoraCreacionArchivo: x.FechaHoraCreacionArchivo,
                ReportCodigo: x.Report.Codigo,
                ReportName: x.Report.Name
            )).ToList());
        }

        if (area.StartsWith("Calidad", StringComparison.OrdinalIgnoreCase))
        {
            var q = _db.RegistrosCalidad
                .AsNoTracking()
                .Include(x => x.Report)
                .AsQueryable();

            if (allowedReportIds != null)
            {
                q = q.Where(x => allowedReportIds.Contains(x.ReportId));
            }

            if (reportId.HasValue)
            {
                q = q.Where(x => x.ReportId == reportId.Value);
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x => x.Lote.Contains(term) ||
                                 (x.Productor != null && x.Productor.Contains(term)));
            }

            var list = await q
                .OrderByDescending(x => x.FechaHoraCreacionArchivo)
                .ThenBy(x => x.Lote)
                .Take(1000)
                .ToListAsync(ct);

            return Ok(list.Select(x => new RecordGridRowDto(
                CodigoOrLote: x.Lote,
                LoteOrProceso: x.Lote,
                Productor: x.Productor,
                Kg: null,
                Calibre: null,
                Estado: "Cargado",
                FechaHoraCreacionArchivo: x.FechaHoraCreacionArchivo,
                ReportCodigo: x.Report.Codigo,
                ReportName: x.Report.Name
            )).ToList());
        }

        // Default: Recepción
        {
            var q = _db.RegistrosRecepcion
                .AsNoTracking()
                .Include(x => x.Report)
                .AsQueryable();

            if (allowedReportIds != null)
            {
                q = q.Where(x => allowedReportIds.Contains(x.ReportId));
            }

            if (reportId.HasValue)
            {
                q = q.Where(x => x.ReportId == reportId.Value);
            }

            if (!string.IsNullOrEmpty(term))
            {
                q = q.Where(x => x.Lote.Contains(term) ||
                                 (x.Productor != null && x.Productor.Contains(term)));
            }

            var list = await q
                .OrderByDescending(x => x.FechaHoraCreacionArchivo)
                .ThenBy(x => x.Lote)
                .Take(1000)
                .ToListAsync(ct);

            return Ok(list.Select(x => new RecordGridRowDto(
                CodigoOrLote: x.Lote,
                LoteOrProceso: x.Lote,
                Productor: x.Productor,
                Kg: x.Kg,
                Calibre: null,
                Estado: "Cargado",
                FechaHoraCreacionArchivo: x.FechaHoraCreacionArchivo,
                ReportCodigo: x.Report.Codigo,
                ReportName: x.Report.Name
            )).ToList());
        }
    }

    private int GetCurrentUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return int.TryParse(raw, out var id) ? id : 0;
    }
}
