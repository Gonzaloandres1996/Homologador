using System.Data;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Homologador.Core.DTOs;
using Microsoft.Data.SqlClient;

namespace Homologador.Core.Services;

public interface IExternalQueryExecutor
{
    Task<QueryExtractResultDto> ExecuteExtractAsync(
        DataSourceConnectDto dataSource,
        int commandType,
        string commandText,
        IReadOnlyDictionary<string, (string SqlType, object? Value)> parameters,
        int maxRows = 100_000,
        CancellationToken ct = default);
}

public class ExternalQueryExecutor : IExternalQueryExecutor
{
    public async Task<QueryExtractResultDto> ExecuteExtractAsync(
        DataSourceConnectDto dataSource,
        int commandType,
        string commandText,
        IReadOnlyDictionary<string, (string SqlType, object? Value)> parameters,
        int maxRows = 100_000,
        CancellationToken ct = default)
    {
        var connectionString = BuildConnectionString(dataSource);
        var sw = Stopwatch.StartNew();

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandTimeout = Math.Max(dataSource.ConnectionTimeout, 30);

        if (commandType == 1) // StoredProcedure
        {
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = CleanStoredProcedureName(commandText);

            foreach (var (paramName, (sqlType, paramValue)) in parameters)
            {
                var cleanName = paramName.StartsWith("@") ? paramName : "@" + paramName;
                var sqlParam = new SqlParameter(cleanName, paramValue ?? DBNull.Value)
                {
                    SqlDbType = MapToSqlDbType(sqlType)
                };
                command.Parameters.Add(sqlParam);
            }
        }
        else // View or SELECT
        {
            command.CommandType = CommandType.Text;
            var cleanViewName = CleanViewName(commandText);

            var whereClauses = new List<string>();
            var index = 0;
            foreach (var (paramName, (sqlType, paramValue)) in parameters)
            {
                if (paramValue != null)
                {
                    var cleanColName = Regex.Replace(paramName, @"[^\w]", "");
                    if (!string.IsNullOrEmpty(cleanColName))
                    {
                        var argName = $"@p_{index++}";
                        whereClauses.Add($"[{cleanColName}] = {argName}");
                        var sqlParam = new SqlParameter(argName, paramValue)
                        {
                            SqlDbType = MapToSqlDbType(sqlType)
                        };
                        command.Parameters.Add(sqlParam);
                    }
                }
            }

            var whereSql = whereClauses.Count > 0 ? " WHERE " + string.Join(" AND ", whereClauses) : "";
            command.CommandText = cleanViewName.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase)
                ? cleanViewName
                : $"SELECT TOP {maxRows} * FROM {cleanViewName}{whereSql}";
        }

        await using var reader = await command.ExecuteReaderAsync(ct);

        var schemaColumns = await reader.GetColumnSchemaAsync(ct);
        var columns = new List<ColumnSchemaDto>();

        foreach (var col in schemaColumns)
        {
            var colName = col.ColumnName?.Trim() ?? $"Col_{columns.Count + 1}";
            columns.Add(new ColumnSchemaDto(
                Name: colName,
                SqlType: col.DataTypeName ?? "nvarchar",
                DotNetType: col.DataType?.Name ?? "Object",
                IsNullable: col.AllowDBNull ?? true,
                ColumnSize: col.ColumnSize
            ));
        }

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct) && rows.Count < maxRows)
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var colName = reader.GetName(i).Trim();
                var val = await reader.IsDBNullAsync(i, ct) ? null : reader.GetValue(i);

                if (val is DateTime dt)
                {
                    row[colName] = dt.ToString("yyyy-MM-ddTHH:mm:ss");
                }
                else if (val is byte[] bytes)
                {
                    row[colName] = Convert.ToBase64String(bytes);
                }
                else
                {
                    row[colName] = val;
                }
            }
            rows.Add(row);
        }

        sw.Stop();
        return new QueryExtractResultDto(rows.Count, columns, rows, sw.ElapsedMilliseconds);
    }

    public static string BuildConnectionString(DataSourceConnectDto dataSource)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = dataSource.Server,
            InitialCatalog = dataSource.Database,
            ConnectTimeout = Math.Max(dataSource.ConnectionTimeout, 10),
            TrustServerCertificate = true
        };

        if (dataSource.AuthType == 1) // SqlAuth
        {
            builder.IntegratedSecurity = false;
            builder.UserID = dataSource.DecryptedUsername ?? string.Empty;
            builder.Password = dataSource.DecryptedPassword ?? string.Empty;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        ApplyDaiaOverride(builder);

        return builder.ConnectionString;
    }

    private static void ApplyDaiaOverride(SqlConnectionStringBuilder builder)
    {
        try
        {
            var settingsPath = ResolveAppSettingsPath();
            if (settingsPath is null || !File.Exists(settingsPath))
            {
                return;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var root = doc.RootElement;

            if (!root.TryGetProperty("DaiaOverrideDatabase", out var dbProp) ||
                dbProp.ValueKind != JsonValueKind.String)
            {
                return;
            }

            var overrideDb = dbProp.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(overrideDb))
            {
                return;
            }

            builder.InitialCatalog = overrideDb;

            if (root.TryGetProperty("DaiaOverrideServer", out var srvProp) &&
                srvProp.ValueKind == JsonValueKind.String)
            {
                var overrideServer = srvProp.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(overrideServer))
                {
                    builder.DataSource = overrideServer;
                }
            }

            if (root.TryGetProperty("DaiaOverrideTrustedConnection", out var trustedProp) &&
                trustedProp.ValueKind is JsonValueKind.True or JsonValueKind.False &&
                trustedProp.GetBoolean())
            {
                builder.IntegratedSecurity = true;
                builder.Remove("User ID");
                builder.Remove("Password");
            }
        }
        catch
        {
            // Ignore malformed or inaccessible local override file and keep original DataSource
        }
    }

    private static string? ResolveAppSettingsPath()
    {
        foreach (var startDir in new[] { AppDomain.CurrentDomain.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(startDir);
            while (dir is not null)
            {
                var repoCandidate = Path.Combine(dir.FullName, "src", "Homologador.AzureApi", "appsettings.json");
                if (File.Exists(repoCandidate))
                {
                    return repoCandidate;
                }
                dir = dir.Parent;
            }
        }

        var localCandidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (File.Exists(localCandidate))
        {
            return localCandidate;
        }

        var cwdCandidate = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        return File.Exists(cwdCandidate) ? cwdCandidate : null;
    }

    private static string CleanStoredProcedureName(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("EXEC ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.Substring(5).Trim();
        }

        var parts = trimmed.Split(' ', 2);
        return parts[0].Trim();
    }

    private static string CleanViewName(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("SELECT ", StringComparison.OrdinalIgnoreCase))
        {
            if (Regex.IsMatch(trimmed, @"\b(DROP|ALTER|TRUNCATE|DELETE|UPDATE|INSERT|CREATE)\b", RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException("Destructive SQL commands are strictly prohibited.");
            }
            return trimmed;
        }

        var clean = Regex.Replace(trimmed, @"[^\w\.\[\]]", "");
        return clean.StartsWith("[") || clean.Contains(".") ? clean : $"[{clean}]";
    }

    private static SqlDbType MapToSqlDbType(string type)
    {
        return type.ToLowerInvariant() switch
        {
            "int" or "integer" => SqlDbType.Int,
            "bigint" => SqlDbType.BigInt,
            "smallint" => SqlDbType.SmallInt,
            "tinyint" => SqlDbType.TinyInt,
            "decimal" or "numeric" => SqlDbType.Decimal,
            "money" => SqlDbType.Money,
            "float" => SqlDbType.Float,
            "real" => SqlDbType.Real,
            "bit" or "boolean" => SqlDbType.Bit,
            "datetime" => SqlDbType.DateTime,
            "datetime2" => SqlDbType.DateTime2,
            "date" => SqlDbType.Date,
            "time" => SqlDbType.Time,
            "varchar" => SqlDbType.VarChar,
            "char" => SqlDbType.Char,
            "nchar" => SqlDbType.NChar,
            "uniqueidentifier" or "guid" => SqlDbType.UniqueIdentifier,
            _ => SqlDbType.NVarChar
        };
    }
}
