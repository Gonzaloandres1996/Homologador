using System.Diagnostics;
using Homologador.Core.DTOs;
using Microsoft.Data.SqlClient;

namespace Homologador.Core.Services;

public interface ISqlConnectionTester
{
    Task<TestConnectionResultDto> TestAsync(
        string server,
        string database,
        int authType,
        string? username,
        string? password,
        int timeoutSeconds,
        CancellationToken ct = default);
}

public class SqlConnectionTester : ISqlConnectionTester
{
    public async Task<TestConnectionResultDto> TestAsync(
        string server,
        string database,
        int authType,
        string? username,
        string? password,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            ConnectTimeout = Math.Max(timeoutSeconds, 5),
            TrustServerCertificate = true
        };

        if (authType == 1) // SqlAuth
        {
            builder.IntegratedSecurity = false;
            builder.UserID = username ?? string.Empty;
            builder.Password = password ?? string.Empty;
        }
        else
        {
            builder.IntegratedSecurity = true;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync(ct);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = 5;
            await command.ExecuteScalarAsync(ct);

            sw.Stop();
            return new TestConnectionResultDto(true, sw.ElapsedMilliseconds, null, DateTime.UtcNow);
        }
        catch (SqlException ex)
        {
            sw.Stop();
            return new TestConnectionResultDto(false, sw.ElapsedMilliseconds, $"SQL Error ({ex.Number}): {ex.Message}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestConnectionResultDto(false, sw.ElapsedMilliseconds, ex.Message, DateTime.UtcNow);
        }
    }
}
