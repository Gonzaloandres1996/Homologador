namespace Homologador.Core.DTOs;

public record LoginRequest(string Email, string Password);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public record UserSessionDto(
    int Id,
    string Codigo,
    string Email,
    string DisplayName,
    string Rol,
    string? Perfil
);

public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    UserSessionDto User
);

public record ReportSummaryDto(
    int Id,
    string Codigo,
    string Name,
    string? Description,
    string TargetArea,
    string QueryName,
    string PipelineName,
    int RuleCount,
    string OutputFormat,
    string? OutputSheetName,
    string OutputFilePattern
);

public record DataSourceConnectDto(
    int Id,
    string Codigo,
    string Name,
    string Server,
    string Database,
    int AuthType,
    string? DecryptedUsername,
    string? DecryptedPassword,
    int ConnectionTimeout
);

public record QueryParamDto(
    int Id,
    int SortOrder,
    string Name,
    string SqlType,
    string? DefaultValue,
    string? Description
);

public record QueryExecutionDto(
    int Id,
    string Codigo,
    string Name,
    int CommandType,
    string CommandText,
    IReadOnlyList<QueryParamDto> Parameters
);

public record PipelineRuleDto(
    int Id,
    int SortOrder,
    string TargetColumn,
    string RuleType,
    string? SourceColumn,
    string? FixedValue,
    int? LookupTableId,
    string? LookupKeyColumn,
    string? LookupValueColumn,
    string? DateFormat,
    string? Validation,
    bool IsRequired,
    bool IsIdentity,
    int? SubstringStart,
    int? SubstringLength
);

public record LookupEntryDto(
    int Id,
    string KeyValue,
    string MappedValue,
    string? ExtraField1
);

public record LookupTableDto(
    int Id,
    string Codigo,
    string Name,
    string? Description,
    IReadOnlyList<LookupEntryDto> Entries
);

public record ExecutionContextDto(
    ReportSummaryDto Report,
    DataSourceConnectDto DataSource,
    QueryExecutionDto Query,
    IReadOnlyList<PipelineRuleDto> Rules,
    IReadOnlyList<LookupTableDto> Lookups,
    IReadOnlyList<string> AlreadyGeneratedKeys
);

public record GeneratedRowItemDto(
    string PrimaryKey,
    string? Productor,
    decimal? Kg,
    string? Calibre
);

public record MarkGeneratedRequest(
    int ReportId,
    string TargetArea,
    int RowsExtracted,
    int RowsOutput,
    decimal DurationSeconds,
    DateTime FechaHoraCreacionArchivo,
    IReadOnlyList<GeneratedRowItemDto> GeneratedRows
);

public record MarkGeneratedResponse(
    int InsertedCount,
    int SkippedDuplicates,
    string Area,
    DateTime SavedAt
);

public record RecordGridRowDto(
    string CodigoOrLote,
    string LoteOrProceso,
    string? Productor,
    decimal? Kg,
    string? Calibre,
    string Estado,
    DateTime FechaHoraCreacionArchivo,
    string ReportCodigo,
    string ReportName
);

public record AdminUserDto(
    int Id,
    string Codigo,
    string Email,
    string DisplayName,
    string Rol,
    string? Perfil,
    bool IsActive,
    string AssignedReportsSummary,
    IReadOnlyList<int> AssignedReportIds
);

public record AdminPipelineDto(
    int Id,
    string Codigo,
    string Name,
    string? Description,
    bool IsActive,
    int RuleCount,
    IReadOnlyList<PipelineRuleDto> Rules
);

public record AdminDataSourceDto(
    int Id,
    string Codigo,
    string Name,
    string Server,
    string Database,
    string AuthMode,
    int ConnectionTimeout,
    bool IsActive
);

public record AdminQueryDto(
    int Id,
    string Codigo,
    string Name,
    string DataSourceCodigo,
    string DataSourceName,
    string CommandTypeText,
    string CommandText,
    int ParameterCount,
    IReadOnlyList<QueryParamDto> Parameters
);

public record ResetPasswordRequest(int UserId, string NewPassword);

public record AssignUserReportsRequest(int UserId, IReadOnlyList<int> ReportIds);

public record TransformationWarningDto(
    int RowIndex,
    string TargetColumn,
    string? SourceColumn,
    string OffendingValue,
    string Reason
);

public record TransformationResultDto(
    int TotalRows,
    int WarningCount,
    IReadOnlyList<string> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    IReadOnlyList<TransformationWarningDto> Warnings,
    long DurationMs
);

public record GeneratedFileResult(
    byte[] FileBytes,
    string ContentType,
    string FileName
);

public record ColumnSchemaDto(
    string Name,
    string SqlType,
    string DotNetType,
    bool IsNullable,
    int? ColumnSize
);

public record QueryExtractResultDto(
    int TotalRows,
    IReadOnlyList<ColumnSchemaDto> Columns,
    IReadOnlyList<Dictionary<string, object?>> Rows,
    long DurationMs
);

public record TestConnectionResultDto(
    bool IsSuccess,
    long LatencyMs,
    string? ErrorMessage,
    DateTime TestedAtUtc
);
