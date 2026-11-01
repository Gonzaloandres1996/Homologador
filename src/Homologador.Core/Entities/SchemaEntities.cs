namespace Homologador.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = "Operator"; // 'Admin' | 'Operator'
    public string? Perfil { get; set; } // 'Recepción' | 'Calidad' | 'Producción' | null
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<UserReport> UserReports { get; set; } = new List<UserReport>();
}

public class UserReport
{
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
}

public class DataSource
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public int AuthType { get; set; } // 0 = WindowsAuth, 1 = SqlAuth
    public string? UsernameEncrypted { get; set; }
    public string? PasswordEncrypted { get; set; }
    public int ConnectionTimeout { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<QueryDefinition> QueryDefinitions { get; set; } = new List<QueryDefinition>();
}

public class QueryDefinition
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DataSourceId { get; set; }
    public DataSource DataSource { get; set; } = null!;
    public int CommandType { get; set; } // 0 = View/Select, 1 = StoredProcedure
    public string CommandText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<QueryParameter> Parameters { get; set; } = new List<QueryParameter>();
}

public class QueryParameter
{
    public int Id { get; set; }
    public int QueryDefinitionId { get; set; }
    public QueryDefinition QueryDefinition { get; set; } = null!;
    public int SortOrder { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public string SqlType { get; set; } = "nvarchar";
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
}

public class LookupTable
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<LookupEntry> Entries { get; set; } = new List<LookupEntry>();
}

public class LookupEntry
{
    public int Id { get; set; }
    public int LookupTableId { get; set; }
    public LookupTable LookupTable { get; set; } = null!;
    public string KeyValue { get; set; } = string.Empty;
    public string MappedValue { get; set; } = string.Empty;
    public string? ExtraField1 { get; set; }
}

public class MappingPipeline
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<MappingRule> Rules { get; set; } = new List<MappingRule>();
}

public class MappingRule
{
    public int Id { get; set; }
    public int PipelineId { get; set; }
    public MappingPipeline Pipeline { get; set; } = null!;
    public int SortOrder { get; set; }
    public string TargetColumn { get; set; } = string.Empty;
    public string RuleType { get; set; } = "Direct";
    public string? SourceColumn { get; set; }
    public string? FixedValue { get; set; }
    public int? LookupTableId { get; set; }
    public LookupTable? LookupTable { get; set; }
    public string? LookupKeyColumn { get; set; }
    public string? LookupValueColumn { get; set; }
    public string? DateFormat { get; set; }
    public string? Validation { get; set; }
    public bool IsRequired { get; set; }
    public bool IsIdentity { get; set; }
    public int? SubstringStart { get; set; }
    public int? SubstringLength { get; set; }
}

public class Report
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TargetArea { get; set; } = "Recepción"; // 'Recepción' | 'Calidad' | 'Producción'
    public int QueryDefinitionId { get; set; }
    public QueryDefinition QueryDefinition { get; set; } = null!;
    public int MappingPipelineId { get; set; }
    public MappingPipeline MappingPipeline { get; set; } = null!;
    public string OutputFormat { get; set; } = "Excel";
    public string? OutputSheetName { get; set; }
    public string OutputFilePattern { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<UserReport> UserReports { get; set; } = new List<UserReport>();
}

public class RegistroRecepcion
{
    public string Lote { get; set; } = string.Empty;
    public DateTime FechaHoraCreacionArchivo { get; set; } = DateTime.UtcNow;
    public string? Productor { get; set; }
    public decimal? Kg { get; set; }
    public string Estado { get; set; } = "Cargado";
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
}

public class RegistroCalidad
{
    public string Lote { get; set; } = string.Empty;
    public DateTime FechaHoraCreacionArchivo { get; set; } = DateTime.UtcNow;
    public string? Productor { get; set; }
    public string Estado { get; set; } = "Cargado";
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
}

public class RegistroProduccion
{
    public string LoteProceso { get; set; } = string.Empty;
    public string CodigoPallet { get; set; } = string.Empty;
    public DateTime FechaHoraCreacionArchivo { get; set; } = DateTime.UtcNow;
    public string? Productor { get; set; }
    public string? Calibre { get; set; }
    public string Estado { get; set; } = "Cargado";
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
}

public class ExecutionLog
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public int RowsExtracted { get; set; }
    public int RowsOutput { get; set; }
    public decimal DurationSeconds { get; set; }
    public string Status { get; set; } = "Success"; // 'Success' | 'Empty' | 'Failed'
    public string? ErrorMessage { get; set; }
}
