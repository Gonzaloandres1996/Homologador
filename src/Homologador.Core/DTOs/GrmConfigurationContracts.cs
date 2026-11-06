namespace Homologador.Core.DTOs;

/// <summary>
/// V4 contract verified against GRM commit c35e1ab58762a3c5d470f4bdf81ce87c47e8958e.
/// GET /api/conversor/configuracion[?tipoProceso=1|2|3], same GRM Calidad base URL
/// and Bearer token as V2/V3. No query returns all configuration visible to the
/// caller; a process query returns only that process and its dependencies.
/// TiposProceso identifies that authorized scope; it is NOT the stored TipoProceso
/// column (which is NULL for V4 rows). No key or plaintext credentials are returned.
/// GRM must authorize each process with its existing corporate permission before
/// selecting Reports -> QueryDefinitions -> QueryParameters/DataSources. Include
/// inactive reports/queries/sources and all parameter IDs, including repeated names. Shared source
/// rows appear once. Return rows ordered by CfgTipo/CfgIdOrigen, without text cleanup.
/// 400 invalid process; 401 invalid JWT; 403 no authorized scope; non-2xx never falls
/// back to local SQL. The local API keeps its existing contracts: ReportsController delegates
/// summaries/execution to GrmRuleReader's explicit V4 methods after its current
/// authorization. Preserve the current empty allowed-area result (HTTP 200 with an
/// empty report list) before calling GRM when the local profile has no report areas.
/// AdminController uses administrative summaries and the mapper's
/// query/source projections; its user-assignment names use configuration.Reports.
/// RecordsController resolves report/area/IsActive from those same models while
/// preserving 401/403/404/410. Administrative lookups reuse the V2 equivalence mapper.
/// </summary>
public sealed record GrmConfigurationResponse(
    IReadOnlyList<int>? TiposProceso, IReadOnlyList<GrmConfigurationRow>? Configuraciones);

/// <summary>
/// idAplicacion=2, CfgTipo in Reports/QueryDefinitions/QueryParameters/DataSources.
/// The logical ID is CfgIdOrigen, never IdEquivalencia. Cfg* retain their source
/// SQL types, NULLs, empty strings and original dates (datetime2(0), no UTC rewrite).
/// Codes may be NULL. CfgName is shared (150 chars); Description 500; CommandText
/// and both encrypted fields MAX. The remaining lengths follow the approved 46
/// column schema. Non-applicable Cfg* fields are NULL. V2/V3 columns are excluded
/// from this response and must remain NULL in the selected V4 rows on the server.
/// Ciphertexts use the existing EncryptionService, not a different cipher/key.
/// </summary>
public sealed record GrmConfigurationRow
{
    public int? IdEquivalencia { get; init; }
    public int? IdAplicacion { get; init; }
    public string? CfgTipo { get; init; }
    public int? CfgIdOrigen { get; init; }
    public string? CfgCodigo { get; init; }
    public string? CfgName { get; init; }
    public string? CfgDescription { get; init; }
    public bool? CfgIsActive { get; init; }
    public DateTime? CfgCreatedAt { get; init; }
    public DateTime? CfgUpdatedAt { get; init; }
    public string? CfgTargetArea { get; init; }
    public int? CfgQueryDefinitionId { get; init; }
    public int? CfgMappingPipelineId { get; init; }
    public string? CfgOutputFormat { get; init; }
    public string? CfgOutputSheetName { get; init; }
    public string? CfgOutputFilePattern { get; init; }
    public int? CfgDataSourceId { get; init; }
    public int? CfgCommandType { get; init; }
    public string? CfgCommandText { get; init; }
    public int? CfgSortOrder { get; init; }
    public string? CfgSqlType { get; init; }
    public string? CfgDefaultValue { get; init; }
    public string? CfgServer { get; init; }
    public string? CfgDatabase { get; init; }
    public int? CfgAuthType { get; init; }
    public string? CfgUsernameEncrypted { get; init; }
    public string? CfgPasswordEncrypted { get; init; }
    public int? CfgConnectionTimeout { get; init; }

    // Never let the record's generated diagnostic text include ciphertexts.
    public override string ToString() => $"Configuracion V4: {CfgTipo}/{CfgIdOrigen}";
}
