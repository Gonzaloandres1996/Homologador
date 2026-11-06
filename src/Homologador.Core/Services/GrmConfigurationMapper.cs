using Homologador.Core.DTOs;
using Homologador.Core.Entities;

namespace Homologador.Core.Services;

/// <summary>Request-scoped configuration reconstructed without an EF context or local SQL.</summary>
public sealed record GrmConfigurationSet(
    IReadOnlyList<Report> Reports,
    IReadOnlyList<QueryDefinition> QueryDefinitions,
    IReadOnlyList<QueryParameter> QueryParameters,
    IReadOnlyList<DataSource> DataSources);

public static class GrmConfigurationMapper
{
    private static InvalidOperationException Invalid() =>
        new("GRM devolvio una configuracion V4 incompleta, incoherente o fuera del proceso solicitado.");

    public static GrmConfigurationSet Map(GrmConfigurationResponse response, int? requestedProcess = null)
    {
        if (response.TiposProceso is null || response.Configuraciones is null
            || response.TiposProceso.Count == 0 || response.TiposProceso.Any(p => p is < 1 or > 3)
            || response.TiposProceso.Distinct().Count() != response.TiposProceso.Count
            || (requestedProcess.HasValue && (response.TiposProceso.Count != 1
                || response.TiposProceso[0] != requestedProcess))) throw Invalid();

        var ids = new HashSet<int>();
        var keys = new HashSet<(string Type, int Id)>();
        var reports = new List<Report>();
        var queries = new List<QueryDefinition>();
        var parameters = new List<QueryParameter>();
        var sources = new List<DataSource>();
        foreach (var row in response.Configuraciones)
        {
            if (row is null || row.IdAplicacion != 2 || row.IdEquivalencia is not > 0
                || !ids.Add(row.IdEquivalencia.Value) || row.CfgIdOrigen is not > 0
                || row.CfgTipo is null || !keys.Add((row.CfgTipo, row.CfgIdOrigen.Value))
                || row.CfgName is null) throw Invalid();
            ValidateShape(row);
            var id = row.CfgIdOrigen.Value;
            switch (row.CfgTipo)
            {
                case "Reports":
                    if (row.CfgTargetArea is null || row.CfgQueryDefinitionId is not > 0
                        || row.CfgMappingPipelineId is not > 0 || row.CfgOutputFormat is null
                        || row.CfgOutputFilePattern is null || row.CfgIsActive is null
                        || row.CfgCreatedAt is null) throw Invalid();
                    var process = ReportProcessMap.ForReport(id);
                    if (!response.TiposProceso.Contains(process.TipoProceso)
                        || row.CfgMappingPipelineId != process.MappingPipelineId
                        || Area(process.TipoProceso) != row.CfgTargetArea) throw Invalid();
                    reports.Add(new Report
                    {
                        Id = id, Codigo = row.CfgCodigo!, Name = row.CfgName,
                        Description = row.CfgDescription, TargetArea = row.CfgTargetArea,
                        QueryDefinitionId = row.CfgQueryDefinitionId.Value,
                        MappingPipelineId = row.CfgMappingPipelineId.Value,
                        OutputFormat = row.CfgOutputFormat, OutputSheetName = row.CfgOutputSheetName,
                        OutputFilePattern = row.CfgOutputFilePattern, IsActive = row.CfgIsActive.Value,
                        CreatedAt = row.CfgCreatedAt.Value, UpdatedAt = row.CfgUpdatedAt
                    });
                    break;
                case "QueryDefinitions":
                    if (row.CfgDataSourceId is not > 0 || row.CfgCommandType is null
                        || row.CfgCommandText is null || row.CfgIsActive is null
                        || row.CfgCreatedAt is null) throw Invalid();
                    queries.Add(new QueryDefinition
                    {
                        Id = id, Codigo = row.CfgCodigo!, Name = row.CfgName,
                        DataSourceId = row.CfgDataSourceId.Value, CommandType = row.CfgCommandType.Value,
                        CommandText = row.CfgCommandText, Description = row.CfgDescription,
                        IsActive = row.CfgIsActive.Value, CreatedAt = row.CfgCreatedAt.Value,
                        UpdatedAt = row.CfgUpdatedAt
                    });
                    break;
                case "QueryParameters":
                    if (row.CfgQueryDefinitionId is not > 0 || row.CfgSortOrder is null
                        || row.CfgSqlType is null) throw Invalid();
                    parameters.Add(new QueryParameter
                    {
                        Id = id, QueryDefinitionId = row.CfgQueryDefinitionId.Value,
                        SortOrder = row.CfgSortOrder.Value, Name = row.CfgName,
                        SqlType = row.CfgSqlType, DefaultValue = row.CfgDefaultValue,
                        Description = row.CfgDescription
                    });
                    break;
                case "DataSources":
                    if (row.CfgServer is null || row.CfgDatabase is null || row.CfgAuthType is null
                        || row.CfgConnectionTimeout is null || row.CfgIsActive is null
                        || row.CfgCreatedAt is null) throw Invalid();
                    sources.Add(new DataSource
                    {
                        Id = id, Codigo = row.CfgCodigo!, Name = row.CfgName,
                        Server = row.CfgServer, Database = row.CfgDatabase,
                        AuthType = row.CfgAuthType.Value, UsernameEncrypted = row.CfgUsernameEncrypted,
                        PasswordEncrypted = row.CfgPasswordEncrypted,
                        ConnectionTimeout = row.CfgConnectionTimeout.Value,
                        IsActive = row.CfgIsActive.Value, CreatedAt = row.CfgCreatedAt.Value,
                        UpdatedAt = row.CfgUpdatedAt
                    });
                    break;
                default: throw Invalid();
            }
        }

        var expectedReports = ReportProcessMap.All.Where(p => response.TiposProceso.Contains(p.TipoProceso))
            .Select(p => p.ReportId).Order().ToArray();
        if (!reports.Select(r => r.Id).Order().SequenceEqual(expectedReports)) throw Invalid();
        var queryMap = queries.ToDictionary(q => q.Id);
        var sourceMap = sources.ToDictionary(s => s.Id);
        foreach (var report in reports)
        {
            if (!queryMap.TryGetValue(report.QueryDefinitionId, out var query)) throw Invalid();
            report.QueryDefinition = query;
        }
        foreach (var query in queries)
        {
            if (!reports.Any(r => r.QueryDefinitionId == query.Id)
                || !sourceMap.TryGetValue(query.DataSourceId, out var source)) throw Invalid();
            query.DataSource = source;
            source.QueryDefinitions.Add(query);
        }
        foreach (var parameter in parameters)
        {
            if (!queryMap.TryGetValue(parameter.QueryDefinitionId, out var query)) throw Invalid();
            parameter.QueryDefinition = query;
            query.Parameters.Add(parameter);
        }
        if (sources.Any(s => !queries.Any(q => q.DataSourceId == s.Id))) throw Invalid();
        return new(reports.OrderBy(r => r.Id).ToArray(), queries.OrderBy(q => q.Id).ToArray(),
            parameters.OrderBy(p => p.Id).ToArray(), sources.OrderBy(s => s.Id).ToArray());
    }

    public static ReportSummaryDto ToReportSummary(Report report, int ruleCount, bool administrative = false)
    {
        var process = ReportProcessMap.ForReport(report.Id);
        return new(report.Id, report.Codigo, report.Name, report.Description, report.TargetArea,
            administrative ? $"[{report.QueryDefinition.Codigo}] {report.QueryDefinition.Name}" : report.QueryDefinition.Name,
            administrative ? $"[{process.Codigo}] {process.Name}" : process.Name,
            ruleCount, report.OutputFormat, report.OutputSheetName, report.OutputFilePattern);
    }

    public static ExecutionContextDto ToExecutionContext(Report report,
        IReadOnlyList<PipelineRuleDto> rules, IEncryptionService encryption)
    {
        var source = report.QueryDefinition.DataSource;
        string? username, password;
        try
        {
            username = string.IsNullOrEmpty(source.UsernameEncrypted) ? null : encryption.Decrypt(source.UsernameEncrypted);
            password = string.IsNullOrEmpty(source.PasswordEncrypted) ? null : encryption.Decrypt(source.PasswordEncrypted);
            if (source.AuthType == 1 && (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)))
                throw new InvalidOperationException();
        }
        catch
        {
            // Neither ciphertext, plaintext, key nor the original exception enters a diagnostic.
            throw new InvalidOperationException("No se pudieron descifrar las credenciales V4 con EncryptionService.");
        }
        var query = report.QueryDefinition;
        var parameters = query.Parameters.OrderBy(p => p.SortOrder)
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase).Select(g => g.First())
            .Select(ToParameter).ToArray();
        return new(ToReportSummary(report, rules.Count),
            new(source.Id, source.Codigo, source.Name, source.Server, source.Database,
                source.AuthType, username, password, source.ConnectionTimeout),
            new(query.Id, query.Codigo, query.Name, query.CommandType, query.CommandText, parameters),
            rules, [], []);
    }

    public static IReadOnlyList<AdminQueryDto> ToAdminQueries(GrmConfigurationSet configuration) =>
        configuration.QueryDefinitions.Select(q => new AdminQueryDto(q.Id, q.Codigo, q.Name,
            q.DataSource.Codigo, q.DataSource.Name, q.CommandType == 1 ? "StoredProcedure" : "View / SELECT",
            q.CommandText, q.Parameters.Count, q.Parameters.OrderBy(p => p.SortOrder).Select(ToParameter).ToArray())).ToArray();

    public static IReadOnlyList<AdminDataSourceDto> ToAdminDataSources(GrmConfigurationSet configuration) =>
        configuration.DataSources.Select(s => new AdminDataSourceDto(s.Id, s.Codigo, s.Name,
            s.Server, s.Database, s.AuthType == 1 ? "SQL Authentication" : "Windows Integrated",
            s.ConnectionTimeout, s.IsActive)).ToArray();

    private static QueryParamDto ToParameter(QueryParameter p) =>
        new(p.Id, p.SortOrder, p.Name, p.SqlType, p.DefaultValue, p.Description);

    private static string Area(int process) => process switch
    {
        1 => "Recepción", 2 => "Calidad", 3 => "Producción", _ => throw Invalid()
    };

    private static void ValidateShape(GrmConfigurationRow r)
    {
        // Non-applicable fields cannot silently disappear while rebuilding a source model.
        var report = r.CfgTipo == "Reports";
        var query = r.CfgTipo == "QueryDefinitions";
        var parameter = r.CfgTipo == "QueryParameters";
        var source = r.CfgTipo == "DataSources";
        if ((!report && !query && !parameter && !source)
            || (!report && (r.CfgTargetArea is not null || r.CfgMappingPipelineId is not null
                || r.CfgOutputFormat is not null || r.CfgOutputSheetName is not null || r.CfgOutputFilePattern is not null))
            || (!report && !parameter && r.CfgQueryDefinitionId is not null)
            || (!query && (r.CfgDataSourceId is not null || r.CfgCommandType is not null || r.CfgCommandText is not null))
            || (!parameter && (r.CfgSortOrder is not null || r.CfgSqlType is not null || r.CfgDefaultValue is not null))
            || (!source && (r.CfgServer is not null || r.CfgDatabase is not null || r.CfgAuthType is not null
                || r.CfgUsernameEncrypted is not null || r.CfgPasswordEncrypted is not null || r.CfgConnectionTimeout is not null))
            || (parameter && (r.CfgCodigo is not null || r.CfgIsActive is not null || r.CfgCreatedAt is not null || r.CfgUpdatedAt is not null))
            || (source && r.CfgDescription is not null)) throw Invalid();
    }
}
