using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Homologador.AzureApi;
using Homologador.AzureApi.Controllers;
using Homologador.AzureApi.Data;
using Homologador.AzureApi.Security;
using Homologador.AzureApi.Services;
using Homologador.Core.DTOs;
using Homologador.Core.Services;
using Homologador.WinForms.ApiClient;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

internal static class V4Checks
{
    // Synthetic values only. No production credentials or production encryption key.
    private const string FakeUser = "__FICTICIO_USUARIO__Ñ";
    private const string FakePassword = "__FICTICIO_CLAVE__á";
    private static EncryptionService Cipher() => new(new string('F', 32));
    private static readonly DateTime Created = new(2026, 9, 23, 14, 56, 23);
    private static readonly DateTime Updated = new(2026, 9, 29, 14, 19, 30);

    internal static async Task<int> RunAsync()
    {
        var count = 0;
        async Task Check(string name, Func<Task> action)
        {
            await action(); count++; Console.WriteLine("PASS V4 " + name);
        }
        await Check("13 rows rebuild original IDs, relations, dates and models", () =>
        {
            var result = GrmConfigurationMapper.Map(Fixture(Cipher()));
            Assert(result.Reports.Select(r => r.Id).SequenceEqual([1, 2, 3]));
            Assert(result.QueryDefinitions.Select(q => q.Id).SequenceEqual([1, 2, 3]));
            Assert(result.QueryParameters.Select(p => p.Id).SequenceEqual([1, 2, 3, 4, 5, 6]));
            Assert(result.DataSources.Single().Id == 1);
            Assert(result.Reports.Select(r => r.QueryDefinitionId).SequenceEqual([1, 3, 2]));
            Assert(result.Reports.Select(r => r.MappingPipelineId).SequenceEqual([1, 3, 2]));
            Assert(result.QueryDefinitions.All(q => ReferenceEquals(q.DataSource, result.DataSources[0])));
            Assert(result.DataSources[0].QueryDefinitions.Count == 3);
            Assert(result.Reports.All(r => ReferenceEquals(r.QueryDefinition,
                result.QueryDefinitions.Single(q => q.Id == r.QueryDefinitionId))));
            Assert(result.DataSources[0].CreatedAt == Created && result.DataSources[0].UpdatedAt == Updated);
            Assert(result.DataSources[0].CreatedAt.Kind == DateTimeKind.Unspecified);
            Assert(result.Reports[0].UpdatedAt is null && result.Reports[0].Description == "");
            Assert(result.Reports[2].Description is null && result.DataSources[0].Name == "DAIA Producción");
            return Task.CompletedTask;
        });
        await Check("NULL, empty text and dynamic output values are preserved", () =>
        {
            var response = Fixture(Cipher());
            var rows = response.Configuraciones!.Select(r => r.CfgTipo == "Reports" && r.CfgIdOrigen == 1
                ? r with { CfgCodigo = null, CfgName = "Nombre Ñ á", CfgOutputFormat = "CSV",
                    CfgOutputSheetName = "", CfgOutputFilePattern = "Cambio_{yyyyMMdd}.csv" } : r).ToArray();
            var config = GrmConfigurationMapper.Map(response with { Configuraciones = rows });
            var summary = GrmConfigurationMapper.ToReportSummary(config.Reports[0], 7);
            Assert(summary.Codigo is null && summary.Name == "Nombre Ñ á" && summary.OutputFormat == "CSV"
                && summary.OutputSheetName == "" && summary.OutputFilePattern == "Cambio_{yyyyMMdd}.csv"
                && summary.RuleCount == 7);
            return Task.CompletedTask;
        });
        await Check("six raw parameters retain duplicate species and execution precedence", () =>
        {
            var cipher = Cipher(); var config = GrmConfigurationMapper.Map(Fixture(cipher));
            var raw = config.QueryParameters.Where(p => p.Name == "@ESPECIE").OrderBy(p => p.SortOrder).ToArray();
            Assert(raw.Length == 2 && raw[0].Id == 2 && raw[0].SortOrder == 1 && raw[0].DefaultValue == "CZ"
                && raw[1].Id == 4 && raw[1].SortOrder == 3 && raw[1].DefaultValue == "Cz");
            var context = GrmConfigurationMapper.ToExecutionContext(config.Reports[0], [], cipher);
            Assert(context.Query.Parameters.Select(p => p.Id).SequenceEqual([1, 2, 3]));
            Assert(context.Query.Parameters[1].DefaultValue == "CZ");
            Assert(GrmConfigurationMapper.ToAdminQueries(config).Single(q => q.Id == 1).ParameterCount == 4);
            return Task.CompletedTask;
        });
        await Check("parameter grouping keeps Trim and case-insensitive first-match behavior", () =>
        {
            var cipher = Cipher(); var fixture = Fixture(cipher);
            var rows = fixture.Configuraciones!.Select(r => r.CfgTipo == "QueryParameters" && r.CfgIdOrigen == 4
                ? r with { CfgName = " @especie " } : r).ToArray();
            var config = GrmConfigurationMapper.Map(fixture with { Configuraciones = rows });
            var context = GrmConfigurationMapper.ToExecutionContext(config.Reports[0], [], cipher);
            Assert(config.QueryParameters.Single(p => p.Id == 4).Name == " @especie ");
            Assert(context.Query.Parameters.Count == 3 && context.Query.Parameters[1].Id == 2);
            return Task.CompletedTask;
        });
        await Check("configuration counts are dynamic rather than permanent 3/3/6/1 gates", () =>
        {
            var fixture = Fixture(Cipher());
            var extra = fixture.Configuraciones!.Single(r => r.CfgTipo == "QueryParameters" && r.CfgIdOrigen == 3)
                with { IdEquivalencia = 9999, CfgIdOrigen = 99, CfgName = "@NUEVO", CfgSortOrder = 9, CfgDefaultValue = null };
            var config = GrmConfigurationMapper.Map(fixture with { Configuraciones = [.. fixture.Configuraciones!, extra] });
            Assert(config.QueryParameters.Count == 7 && config.QueryParameters.Single(p => p.Id == 99).DefaultValue is null);
            return Task.CompletedTask;
        });
        foreach (var (reportId, process) in new[] { (1, 2), (2, 1), (3, 3) })
            await Check($"report {reportId} builds the existing connection/query DTO through process {process}", async () =>
            {
                var cipher = Cipher(); var response = Scope(Fixture(cipher), process);
                var mock = new MockHandler(request =>
                {
                    Assert(request.Headers.Authorization?.ToString() == "Bearer same-v2-jwt");
                    Assert(request.RequestUri!.Authority == new Uri(AzureApiHost.GrmCalidadBaseUrl).Authority);
                    return request.RequestUri.PathAndQuery switch
                    {
                        var path when path == $"/api/conversor/configuracion?tipoProceso={process}" =>
                            new(HttpStatusCode.OK) { Content = JsonContent.Create(response) },
                        var path when path == $"/api/conversor/reglas?tipoProceso={process}" =>
                            new(HttpStatusCode.OK) { Content = JsonContent.Create(Rules(process)) },
                        _ => throw new Exception("Unexpected GRM route")
                    };
                });
                using var http = new HttpClient(mock) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
                var context = await new GrmRuleReader(http).GetExecutionContextV4Async(reportId, "Bearer same-v2-jwt", cipher);
                Assert(mock.Calls == 2 && context.Report.Id == reportId && context.Rules.Count == 1);
                Assert(context.DataSource == new DataSourceConnectDto(1, "0001", "DAIA Producción",
                    "192.168.3.17", "daia", 1, FakeUser, FakePassword, 15));
                var query = response.Configuraciones!.Single(r => r.CfgTipo == "QueryDefinitions");
                Assert(context.Query.Id == query.CfgIdOrigen && context.Query.CommandType == query.CfgCommandType
                    && context.Query.CommandText == query.CfgCommandText);
                Assert(context.AlreadyGeneratedKeys.Count == 0 && context.Lookups.Count == 0);
                Assert(context.Report.OutputFormat == "Excel");
            });
        await Check("inactive query/source flags do not create new execution blockers", async () =>
        {
            var cipher = Cipher(); var fixture = Scope(Fixture(cipher), 2);
            var rows = fixture.Configuraciones!.Select(r => r.CfgTipo == "DataSources" ? r with { CfgIsActive = false } : r).ToArray();
            var config = GrmConfigurationMapper.Map(fixture with { Configuraciones = rows }, 2);
            Assert(!config.QueryDefinitions[0].IsActive && !config.DataSources[0].IsActive);
            Assert(GrmConfigurationMapper.ToExecutionContext(config.Reports[0], [], cipher).Query.Id == 1);
            return;
        });
        await Check("report summaries and admin projections keep their contracts", async () =>
        {
            var fixture = Fixture(Cipher());
            var rows = fixture.Configuraciones!.Select(r => r.CfgTipo == "Reports" && r.CfgIdOrigen == 3
                ? r with { CfgIsActive = false } : r).ToArray();
            var response = fixture with { Configuraciones = rows };
            var mock = new MockHandler(request => request.RequestUri!.AbsolutePath == "/api/conversor/configuracion"
                ? new(HttpStatusCode.OK) { Content = JsonContent.Create(response) }
                : new(HttpStatusCode.OK) { Content = JsonContent.Create(Rules(int.Parse(request.RequestUri.Query[^1..]))) });
            using var http = new HttpClient(mock) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
            var reader = new GrmRuleReader(http);
            var menu = await reader.GetReportSummariesV4Async("Bearer token");
            var admin = await reader.GetReportSummariesV4Async("Bearer token", administrative: true);
            Assert(menu.Select(r => r.Id).SequenceEqual([1, 2]) && admin.Count == 3);
            Assert(menu[0].QueryName == "Calidad ANDINEXIA sql"
                && admin[0].QueryName == "[0001] Calidad ANDINEXIA sql");
            var config = GrmConfigurationMapper.Map(response);
            var sources = GrmConfigurationMapper.ToAdminDataSources(config);
            var queries = GrmConfigurationMapper.ToAdminQueries(config);
            Assert(sources.Single().AuthMode == "SQL Authentication" && queries.Count == 3
                && queries.Single(q => q.Id == 1).Parameters.Count == 4);
            var serialized = JsonSerializer.Serialize(sources);
            Assert(!serialized.Contains(FakeUser) && !serialized.Contains(FakePassword)
                && !serialized.Contains("Encrypted", StringComparison.Ordinal));
        });
        await Check("V4 DTO composes with the unchanged desktop client and V2 homologations", async () =>
        {
            var cipher = Cipher(); var fixture = Scope(Fixture(cipher), 1);
            var paths = new List<string>(); string? forwardedToken = null;
            var grm = new MockHandler(request =>
            {
                var token = request.Headers.Authorization?.Parameter;
                forwardedToken ??= token;
                Assert(token == forwardedToken && !string.IsNullOrEmpty(token));
                paths.Add(request.RequestUri!.AbsolutePath);
                return request.RequestUri.AbsolutePath switch
                {
                    "/api/conversor/configuracion" => new(HttpStatusCode.OK) { Content = JsonContent.Create(fixture) },
                    "/api/conversor/reglas" => new(HttpStatusCode.OK) { Content = JsonContent.Create(Rules(1, homologation: true)) },
                    "/api/conversor/equivalencias" => new(HttpStatusCode.OK) { Content = JsonContent.Create(new[] { new GrmEquivalenceRow(2, "Cereza", "CEREZAS") }) },
                    _ => throw new Exception("Unexpected route")
                };
            });
            using var serverHttp = new HttpClient(grm, disposeHandler: false) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
            var reader = new GrmRuleReader(serverHttp);
            var local = new MockHandler(request =>
            {
                Assert(request.RequestUri!.AbsolutePath == "/api/reports/2/execution-context");
                var context = reader.GetExecutionContextV4Async(2, request.Headers.Authorization!.ToString(), cipher).GetAwaiter().GetResult();
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(context) };
            });
            var auth = new HttpClient(new MockHandler(_ => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { accessToken = new JwtSecurityTokenHandler().WriteToken(
                    new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(1))), expiresIn = 3600, tokenType = "Bearer" })
            }));
            var tokens = new GrmTokenManager(auth); await tokens.LoginAsync("fake", "fake");
            using var client = new HomologadorApiClient(new HttpClient(local), new HttpClient(grm), tokens, "http://localhost:5080");
            var loaded = await client.GetExecutionContextAsync(2);
            Assert(paths.SequenceEqual(["/api/conversor/configuracion", "/api/conversor/reglas", "/api/conversor/equivalencias"]));
            var result = new TransformationEngine().Transform(loaded.Rules, loaded.Lookups,
                [new Dictionary<string, object?> { ["Especie"] = "Cereza" }]);
            Assert((string?)result.Rows.Single()["ESPECIE"] == "CEREZAS" && result.WarningCount == 0);
            var empty = new TransformationEngine().Transform(loaded.Rules, loaded.Lookups, []);
            Assert(empty.Rows.Count == 0 && local.Calls == 1);
        });
        foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
            await Check($"HTTP {status} stops before rules without local fallback or secret diagnostics", async () =>
            {
                var mock = new MockHandler(_ => new(status) { Content = new StringContent(FakePassword) });
                using var http = new HttpClient(mock) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
                var expected = status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? (int)status : 503;
                var error = await Failure(() => new GrmRuleReader(http).GetExecutionContextV4Async(1, "Bearer token", Cipher()), expected);
                Assert(mock.Calls == 1 && !error.ToString().Contains(FakePassword));
            });
        foreach (var mode in new[] { "timeout", "offline", "json", "null", "missing" })
            await Check($"{mode} configuration blocks context without fallback", async () =>
            {
                var mock = new MockHandler(_ => mode switch
                {
                    "timeout" => throw new TaskCanceledException(),
                    "offline" => throw new HttpRequestException(FakePassword),
                    "json" => new(HttpStatusCode.OK) { Content = new StringContent("{bad json") },
                    "null" => new(HttpStatusCode.OK) { Content = new StringContent("null") },
                    _ => new(HttpStatusCode.OK) { Content = JsonContent.Create(new GrmConfigurationResponse([2], [])) }
                });
                using var http = new HttpClient(mock) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
                var error = await Failure(() => new GrmRuleReader(http).GetExecutionContextV4Async(1, "Bearer token", Cipher()),
                    mode is "timeout" or "offline" ? 503 : 502);
                Assert(mock.Calls == 1 && error.InnerException is null && !error.ToString().Contains(FakePassword));
            });
        await Check("missing JWT, unknown report and invalid process do not send requests", async () =>
        {
            var mock = new MockHandler(_ => throw new Exception("Unexpected HTTP call"));
            using var http = new HttpClient(mock) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
            var reader = new GrmRuleReader(http);
            await Failure(() => reader.GetConfigurationV4Async(""), 401);
            await Failure(() => reader.GetRulesAsync(99, "", default), 401);
            await Failure(() => reader.GetExecutionContextV4Async(99, "", Cipher()), 401);
            await Failure(() => reader.GetExecutionContextV4Async(99, "Bearer token", Cipher()), 404);
            await Failure(() => reader.GetConfigurationV4Async("Bearer token", 4), 400);
            Assert(mock.Calls == 0);
        });
        await Check("decryption errors and DTO diagnostics never expose secrets", async () =>
        {
            var fixture = Scope(Fixture(Cipher()), 2);
            var rows = fixture.Configuraciones!.Select(r => r.CfgTipo == "DataSources"
                ? r with { CfgPasswordEncrypted = "not-a-cipher-" + FakePassword } : r).ToArray();
            var response = fixture with { Configuraciones = rows };
            var mock = new MockHandler(request => request.RequestUri!.AbsolutePath == "/api/conversor/configuracion"
                ? new(HttpStatusCode.OK) { Content = JsonContent.Create(response) }
                : new(HttpStatusCode.OK) { Content = JsonContent.Create(Rules(2)) });
            using var http = new HttpClient(mock) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
            var error = await Failure(() => new GrmRuleReader(http).GetExecutionContextV4Async(1, "Bearer token", Cipher()), 502);
            Assert(error.InnerException is null && !error.ToString().Contains(FakePassword));
            var row = response.Configuraciones!.Single(r => r.CfgTipo == "DataSources");
            Assert(!row.ToString().Contains(row.CfgPasswordEncrypted!, StringComparison.Ordinal));
        });
        await Check("contradictory scopes, duplicate IDs, orphan relations and shape errors are rejected", () =>
        {
            var fixture = Fixture(Cipher());
            foreach (var invalid in new[]
            {
                fixture with { TiposProceso = [2] },
                fixture with { TiposProceso = [1, 1, 2, 3] },
                fixture with { Configuraciones = fixture.Configuraciones!.Where(r => r.CfgTipo != "DataSources").ToArray() },
                fixture with { Configuraciones = [.. fixture.Configuraciones!, fixture.Configuraciones![0]] },
                Change(fixture, "Reports", 1, r => r with { CfgMappingPipelineId = 3 }),
                Change(fixture, "Reports", 1, r => r with { CfgQueryDefinitionId = 999 }),
                Change(fixture, "QueryDefinitions", 1, r => r with { CfgDataSourceId = 999 }),
                Change(fixture, "QueryParameters", 1, r => r with { CfgQueryDefinitionId = 999 }),
                Change(fixture, "Reports", 1, r => r with { IdAplicacion = 3 }),
                Change(fixture, "DataSources", 1, r => r with { CfgTargetArea = "Calidad" }),
                Change(fixture, "QueryParameters", 1, r => r with { CfgIsActive = false }),
                Change(fixture, "Reports", 1, r => r with { CfgName = null })
            })
            {
                try { GrmConfigurationMapper.Map(invalid); throw new Exception("Invalid configuration accepted"); }
                catch (InvalidOperationException) { }
            }
            try { GrmConfigurationMapper.Map(Fixture(Cipher()), 2); throw new Exception("Overbroad scope accepted"); }
            catch (InvalidOperationException) { }
            return Task.CompletedTask;
        });
        await Check("host and active controllers resolve without any ConfigDb registration", async () =>
        {
            await using var app = AzureApiHost.Build(["--ConnectionStrings:ConfigDb=invalid-unused-connection"]);
            using var scope = app.Services.CreateScope();
            Assert(scope.ServiceProvider.GetService<ConfigDbContext>() is null);
            Assert(ActivatorUtilities.CreateInstance<ReportsController>(scope.ServiceProvider) is not null);
            Assert(ActivatorUtilities.CreateInstance<RecordsController>(scope.ServiceProvider) is not null);
            Assert(ActivatorUtilities.CreateInstance<AdminController>(scope.ServiceProvider) is not null);
        });
        await Check("active report menu reads V4 and V3 for all three original reports", async () =>
        {
            using var h = new ActiveFlow();
            var reports = Value(await h.Reports().GetMyReports(default));
            Assert(reports.Select(r => r.Id).SequenceEqual([1, 2, 3]));
            Assert(reports.Select(r => r.TargetArea).SequenceEqual(["Calidad", "Recepción", "Producción"]));
            Assert(h.Paths.SequenceEqual(["/api/conversor/configuracion",
                "/api/conversor/reglas?tipoProceso=2", "/api/conversor/reglas?tipoProceso=1",
                "/api/conversor/reglas?tipoProceso=3"]));
        });
        await Check("profile permissions filter reports before requesting any unauthorized rules", async () =>
        {
            using var h = new ActiveFlow(areas: ["Calidad"]);
            Assert(Value(await h.Reports().GetMyReports(default)).Single().Id == 1);
            Assert(h.Paths.SequenceEqual(["/api/conversor/configuracion", "/api/conversor/reglas?tipoProceso=2"]));
            using var partial = new ActiveFlow(areas: ["Calidad"], configuration: Scope(Fixture(Cipher()), 2));
            Assert(Value(await partial.Reports().GetMyReports(default)).Single().Id == 1);
        });
        await Check("profile with no areas keeps HTTP 200 empty menu without configuration reads", async () =>
        {
            using var h = new ActiveFlow(areas: []);
            Assert(Value(await h.Reports().GetMyReports(default)).Count == 0 && h.Paths.Count == 0);
        });
        foreach (var (reportId, process) in new[] { (1, 2), (2, 1), (3, 3) })
            await Check($"active controller executes report {reportId} with process {process} and existing DTOs", async () =>
            {
                using var h = new ActiveFlow();
                var context = Value(await h.Reports().GetExecutionContext(reportId, default));
                Assert(context.Report.Id == reportId && context.DataSource.DecryptedUsername == FakeUser
                    && context.DataSource.DecryptedPassword == FakePassword && context.DataSource.ConnectionTimeout == 15);
                Assert(h.Paths.SequenceEqual([$"/api/conversor/configuracion?tipoProceso={process}",
                    $"/api/conversor/reglas?tipoProceso={process}"]));
                if (reportId == 1)
                    Assert(context.Query.Parameters.Count == 3 && context.Query.Parameters[1].DefaultValue == "CZ");
            });
        await Check("controller authorization blocks mismatched area, inactive report and missing JWT", async () =>
        {
            using var restricted = new ActiveFlow(areas: ["Calidad"]);
            Assert((await restricted.Reports().GetExecutionContext(2, default)).Result is ForbidResult);
            Assert(restricted.Paths.Count == 1);
            using var inactive = new ActiveFlow(configuration:
                Change(Fixture(Cipher()), "Reports", 1, r => r with { CfgIsActive = false }));
            Assert((await inactive.Reports().GetExecutionContext(1, default)).Result is ForbidResult);
            Assert(Value(await inactive.Reports().GetMyReports(default)).Select(r => r.Id).SequenceEqual([2, 3]));
            Assert(Value(await inactive.Admin().GetReports(default)).Count == 3);
            var noJwt = restricted.Reports(); noJwt.Request.Headers.Authorization = "";
            Assert(Status(await noJwt.GetMyReports(default)) == 401);
            var unknown = await restricted.Reports().GetExecutionContext(99, default);
            Assert(Status(unknown) == 404);
        });
        await Check("active admin contracts expose V4 metadata, six raw parameters and V2 lookups without secrets", async () =>
        {
            using var h = new ActiveFlow();
            var admin = h.Admin();
            var reports = Value(await admin.GetReports(default));
            var queries = Value(await admin.GetQueries(default));
            var sources = Value(await admin.GetDataSources(default));
            var lookups = Value(await admin.GetLookups(default));
            Assert(reports.Count == 3 && reports.All(r => r.QueryName.StartsWith("[")));
            Assert(queries.Count == 3 && queries.Sum(q => q.Parameters.Count) == 6);
            Assert(queries.Single(q => q.Id == 1).Parameters.Where(p => p.Name == "@ESPECIE")
                .Select(p => p.DefaultValue).SequenceEqual(["CZ", "Cz"]));
            Assert(sources.Single().ConnectionTimeout == 15);
            Assert(lookups.Select(l => l.Id).SequenceEqual([1, 2, 3, 4]) && lookups.Sum(l => l.Entries.Count) == 7);
            var json = JsonSerializer.Serialize(new { reports, queries, sources, lookups });
            Assert(!json.Contains(FakeUser) && !json.Contains(FakePassword) && !json.Contains("Encrypted"));
        });
        await Check("legacy records keep 410 and authorization contracts using only V4 metadata", async () =>
        {
            using var h = new ActiveFlow();
            Assert(Status(await h.Records().GetByReport(1)) == 410);
            Assert(Status(await h.Records().GetByArea("Produccion", reportId: 3)) == 410);
            Assert((await h.Records().GetByArea("Calidad", reportId: 2)).Result is ForbidResult);
            Assert(Status(await h.Records().GetByReport(99)) == 404);
            Assert(Status(await h.Records().MarkGenerated(new(1, "Calidad", 0, 0, 0, Created, []), default)) == 410);
            Assert((await h.Records().MarkGenerated(new(1, "Recepción", 0, 0, 0, Created, []), default)).Result is ForbidResult);
            Assert(h.Paths.All(p => p.StartsWith("/api/conversor/configuracion")));
            using var restricted = new ActiveFlow(areas: ["Calidad"]);
            Assert((await restricted.Records().GetByArea("Recepción")).Result is ForbidResult);
            Assert(restricted.Paths.Count == 0);
        });
        foreach (var upstream in new[] { HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
            await Check($"active controllers propagate GRM {upstream} without local fallback", async () =>
            {
                using var h = new ActiveFlow(configurationStatus: upstream);
                var expected = upstream is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden ? (int)upstream : 503;
                Assert(Status(await h.Reports().GetMyReports(default)) == expected);
                Assert(Status(await h.Reports().GetExecutionContext(1, default)) == expected);
                Assert(Status(await h.Admin().GetQueries(default)) == expected);
                Assert(Status(await h.Admin().GetDataSources(default)) == expected);
                Assert(Status(await h.Admin().GetReports(default)) == expected);
                Assert(Status(await h.Records().GetByReport(1)) == expected);
                Assert(h.Paths.Count == 6 && h.Paths.All(p => p.StartsWith("/api/conversor/configuracion")));
            });
        await Check("incomplete V4 data blocks active controllers before any rules or partial output", async () =>
        {
            var fixture = Fixture(Cipher());
            using var h = new ActiveFlow(configuration: fixture with
                { Configuraciones = fixture.Configuraciones!.Where(r => r.CfgTipo != "DataSources").ToArray() });
            Assert(Status(await h.Reports().GetMyReports(default)) == 502);
            Assert(Status(await h.Reports().GetExecutionContext(1, default)) == 502);
            Assert(Status(await h.Admin().GetQueries(default)) == 502);
            Assert(Status(await h.Records().GetByReport(1)) == 502);
            Assert(h.Paths.Count == 4 && h.Paths.All(p => p.StartsWith("/api/conversor/configuracion")));
        });
        await Check("administrative V2 read rejects incomplete equivalences and propagates GRM failures", async () =>
        {
            foreach (var status in new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.Unauthorized,
                HttpStatusCode.Forbidden, HttpStatusCode.OK })
            {
                var mock = new MockHandler(_ => new(status)
                    { Content = JsonContent.Create(new[] { new GrmEquivalenceRow(2, "Cereza", "CEREZAS") }) });
                using var http = new HttpClient(mock) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
                var admin = new AdminController(null!, new GrmRuleReader(http))
                    { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
                admin.Request.Headers.Authorization = "Bearer controlled-jwt";
                var expected = status == HttpStatusCode.OK ? 502 : status == HttpStatusCode.ServiceUnavailable ? 503 : (int)status;
                Assert(Status(await admin.GetLookups(default)) == expected && mock.Calls == 1);
            }
        });
        await Check("desktop consumes the active ReportsController, V4, V3 and V2 on the existing clients", async () =>
        {
            using var h = new ActiveFlow();
            using var loginHttp = new HttpClient(new MockHandler(_ => new(HttpStatusCode.OK)
            { Content = JsonContent.Create(new { accessToken = new JwtSecurityTokenHandler().WriteToken(
                new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(1))), expiresIn = 3600, tokenType = "Bearer" }) }));
            var tokens = new GrmTokenManager(loginHttp); await tokens.LoginAsync("fake", "fake");
            var local = new MockHandler(request =>
            {
                var controller = h.Reports();
                controller.Request.Headers.Authorization = request.Headers.Authorization!.ToString();
                var result = controller.GetExecutionContext(2, default).GetAwaiter().GetResult();
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(Value(result)) };
            });
            using var client = new HomologadorApiClient(new HttpClient(local), h.Http, tokens, "http://localhost:5080");
            var loaded = await client.GetExecutionContextAsync(2);
            Assert(loaded.Report.Id == 2 && loaded.Query.Id == 3 && loaded.Lookups.Single().Id == 1);
            var transformed = new TransformationEngine().Transform(loaded.Rules, loaded.Lookups,
                [new Dictionary<string, object?> { ["Especie"] = "Cereza" }]);
            Assert((string?)transformed.Rows.Single()["ESPECIE"] == "CEREZAS");
            Assert(new TransformationEngine().Transform(loaded.Rules, loaded.Lookups, []).Rows.Count == 0);
            Assert(h.Paths.SequenceEqual(["/api/conversor/configuracion?tipoProceso=1",
                "/api/conversor/reglas?tipoProceso=1", "/api/conversor/equivalencias"]));
        });
        return count;
    }

    private static T Value<T>(ActionResult<T> result) => (T)((OkObjectResult)result.Result!).Value!;
    private static int Status<T>(ActionResult<T> result) => result.Result switch
    {
        ObjectResult obj => obj.StatusCode!.Value,
        StatusCodeResult status => status.StatusCode,
        ForbidResult => 403,
        _ => throw new Exception("Unexpected result")
    };

    private sealed class ActiveFlow : IDisposable
    {
        public List<string> Paths { get; } = [];
        public HttpClient Http { get; }
        private readonly HttpClient profileHttp;
        private readonly GrmProfileAccess profile;
        private readonly GrmRuleReader reader;
        private readonly EncryptionService cipher = Cipher();

        public ActiveFlow(string[]? areas = null, GrmConfigurationResponse? configuration = null,
            HttpStatusCode configurationStatus = HttpStatusCode.OK)
        {
            areas ??= ["Calidad", "Recepción", "Producción"];
            var identities = new[] { ("Calidad", "Calidad", "CALIDAD"), ("Recepción", "Recepcion", "RECEPCION"),
                ("Producción", "Produccion", "PRODUCCION") }.Where(a => areas.Contains(a.Item1)).ToArray();
            profileHttp = new(new MockHandler(request =>
            {
                Assert(request.RequestUri!.AbsolutePath.EndsWith("/user"));
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new GrmUser(1, "fake@example.invalid",
                    "fake", "fake", "fake", new GrmCompany(1),
                    identities.Select(a => "Conversor_" + a.Item2).ToArray(),
                    identities.Select(a => "GRM.CONVERSOR." + a.Item3).ToArray())) };
            })) { BaseAddress = new(AzureApiHost.GrmAuthenticationBaseUrl) };
            profile = new(profileHttp);
            configuration ??= Fixture(cipher);
            Http = new(new MockHandler(request =>
            {
                Assert(request.Method == HttpMethod.Get && request.Headers.Authorization?.Scheme == "Bearer");
                Paths.Add(request.RequestUri!.PathAndQuery);
                var path = request.RequestUri.AbsolutePath;
                if (path == "/api/conversor/equivalencias")
                    return new(HttpStatusCode.OK) { Content = JsonContent.Create(new GrmEquivalenceRow[]
                    { new(2,"Cereza","CEREZAS"),new(2,"Ciruela","CIRUELAS"),new(3,"no","No Cumple"),
                        new(3,"si","Cumple"),new(4,"no","Cumple"),new(4,"si","No Cumple"),new(5,"Si","Hidrocooler") }) };
                var process = request.RequestUri.Query.Length == 0 ? (int?)null : int.Parse(request.RequestUri.Query.Split('=')[1]);
                if (path == "/api/conversor/configuracion")
                    return new(configurationStatus) { Content = JsonContent.Create(process.HasValue ? Scope(configuration, process.Value) : configuration) };
                if (path == "/api/conversor/reglas")
                    return new(HttpStatusCode.OK) { Content = JsonContent.Create(Rules(process!.Value, homologation: true)) };
                throw new Exception("Unexpected GRM call");
            })) { BaseAddress = new(AzureApiHost.GrmCalidadBaseUrl) };
            reader = new(Http);
        }

        private static T Context<T>(T controller) where T : ControllerBase
        {
            controller.ControllerContext = new() { HttpContext = new DefaultHttpContext() };
            controller.Request.Headers.Authorization = "Bearer controlled-jwt";
            return controller;
        }
        public ReportsController Reports() => Context(new ReportsController(cipher, profile, reader));
        public AdminController Admin() => Context(new AdminController(null!, reader));
        public RecordsController Records() => Context(new RecordsController(reader, profile));
        public void Dispose() { Http.Dispose(); profileHttp.Dispose(); }
    }

    private static void Assert(bool condition) { if (!condition) throw new Exception("V4 assertion failed."); }
    private static async Task<GrmRuleFetchException> Failure(Func<Task> action, int status)
    {
        try { await action(); }
        catch (GrmRuleFetchException ex) { Assert(ex.StatusCode == status); return ex; }
        throw new Exception("Expected V4 failure was not raised.");
    }
    private static GrmConfigurationResponse Change(GrmConfigurationResponse fixture, string type, int id,
        Func<GrmConfigurationRow, GrmConfigurationRow> change) => fixture with
        { Configuraciones = fixture.Configuraciones!.Select(r => r.CfgTipo == type && r.CfgIdOrigen == id ? change(r) : r).ToArray() };

    private static GrmConfigurationResponse Fixture(EncryptionService cipher)
    {
        GrmConfigurationRow Row(string type, int id, string name, int identity) => new()
        { IdAplicacion = 2, IdEquivalencia = identity, CfgTipo = type, CfgIdOrigen = id, CfgName = name };
        return new([1, 2, 3], [
            Row("Reports",1,"Calidad ANDINEXIA",901) with { CfgCodigo="0001",CfgDescription="",CfgTargetArea="Calidad",CfgQueryDefinitionId=1,CfgMappingPipelineId=1,CfgOutputFormat="Excel",CfgOutputSheetName="Data",CfgOutputFilePattern="Calidad_{yyyyMMdd_HHmmss}.xlsx",CfgIsActive=true,CfgCreatedAt=Created,CfgUpdatedAt=null },
            Row("Reports",2,"Carga Masiva SAP Recepción MP",902) with { CfgCodigo="0002",CfgDescription="Planilla homologada lista para SAP a partir de DAIA",CfgTargetArea="Recepción",CfgQueryDefinitionId=3,CfgMappingPipelineId=3,CfgOutputFormat="Excel",CfgOutputSheetName="RECEPCION",CfgOutputFilePattern="SAP_RECEPCION_{yyyyMMdd_HHmmss}.xlsx",CfgIsActive=true,CfgCreatedAt=Created,CfgUpdatedAt=Updated },
            Row("Reports",3,"Producción ANDINEXIA",903) with { CfgCodigo="0003",CfgDescription=null,CfgTargetArea="Producción",CfgQueryDefinitionId=2,CfgMappingPipelineId=2,CfgOutputFormat="Excel",CfgOutputSheetName="PRODUCCION",CfgOutputFilePattern="Proceso_{yyyyMMdd_HHmmss}.xlsx",CfgIsActive=true,CfgCreatedAt=Created,CfgUpdatedAt=Updated },
            Row("QueryDefinitions",1,"Calidad ANDINEXIA sql",904) with { CfgCodigo="0001",CfgDataSourceId=1,CfgCommandType=1,CfgCommandText="[dbo].[SPC_CALIDAD_PIVOT]",CfgDescription="Consulta pivot de calidad Andinexia",CfgIsActive=false,CfgCreatedAt=Created,CfgUpdatedAt=Updated },
            Row("QueryDefinitions",2,"Producción ANDINEXIA sql",905) with { CfgCodigo="0002",CfgDataSourceId=1,CfgCommandType=1,CfgCommandText="EXEC [dbo].[SP_AUTH_VIEW] @view_name = N'viewProduccion'",CfgDescription="Consulta pivot de producción Andinexia",CfgIsActive=false,CfgCreatedAt=Created,CfgUpdatedAt=Updated },
            Row("QueryDefinitions",3,"Recepción Materia Prima (DAIA)",906) with { CfgCodigo="0003",CfgDataSourceId=1,CfgCommandType=1,CfgCommandText="EXEC [dbo].[SP_AUTH_VIEW] @view_name = N'viewRecepcion'",CfgDescription="Extracción de recepciones",CfgIsActive=true,CfgCreatedAt=Created,CfgUpdatedAt=null },
            Row("QueryParameters",1,"@CONTROL_TYPE",907) with { CfgQueryDefinitionId=1,CfgSortOrder=0,CfgSqlType="nvarchar",CfgDefaultValue="RCP",CfgDescription="Tipo de control" },
            Row("QueryParameters",2,"@ESPECIE",908) with { CfgQueryDefinitionId=1,CfgSortOrder=1,CfgSqlType="nvarchar",CfgDefaultValue="CZ",CfgDescription="Especie" },
            Row("QueryParameters",3,"@TEMPORADA",909) with { CfgQueryDefinitionId=1,CfgSortOrder=2,CfgSqlType="nvarchar",CfgDefaultValue="6",CfgDescription="Temporada" },
            Row("QueryParameters",4,"@ESPECIE",910) with { CfgQueryDefinitionId=1,CfgSortOrder=3,CfgSqlType="nvarchar",CfgDefaultValue="Cz",CfgDescription="" },
            Row("QueryParameters",5,"@view_name",911) with { CfgQueryDefinitionId=2,CfgSortOrder=0,CfgSqlType="nvarchar",CfgDefaultValue="viewProduccion",CfgDescription="" },
            Row("QueryParameters",6,"@view_name",912) with { CfgQueryDefinitionId=3,CfgSortOrder=0,CfgSqlType="nvarchar",CfgDefaultValue="viewRecepcion",CfgDescription="Nombre de la vista origen" },
            Row("DataSources",1,"DAIA Producción",913) with { CfgCodigo="0001",CfgServer="192.168.3.17",CfgDatabase="daia",CfgAuthType=1,CfgConnectionTimeout=15,CfgIsActive=true,CfgCreatedAt=Created,CfgUpdatedAt=Updated,CfgUsernameEncrypted=cipher.Encrypt(FakeUser),CfgPasswordEncrypted=cipher.Encrypt(FakePassword) }
        ]);
    }
    private static GrmConfigurationResponse Scope(GrmConfigurationResponse response, int process)
    {
        var reportId = ReportProcessMap.All.Single(p => p.TipoProceso == process).ReportId;
        var rows = response.Configuraciones!;
        var report = rows.Single(r => r.CfgTipo == "Reports" && r.CfgIdOrigen == reportId);
        var query = rows.Single(r => r.CfgTipo == "QueryDefinitions" && r.CfgIdOrigen == report.CfgQueryDefinitionId);
        return new([process], rows.Where(r => ReferenceEquals(r, report) || ReferenceEquals(r, query)
            || (r.CfgTipo == "QueryParameters" && r.CfgQueryDefinitionId == query.CfgIdOrigen)
            || (r.CfgTipo == "DataSources" && r.CfgIdOrigen == query.CfgDataSourceId)).ToArray());
    }
    private static GrmRulesResponse Rules(int process, bool homologation = false) => new(process,
        [new GrmRuleRow { IdEquivalencia = 9900+process, IdAplicacion=2, TipoProceso=process,
            Valor1="1",Valor2=homologation?"ESPECIE":"C1",Valor3=homologation?"Homologation":"Empty",
            Valor4=homologation?"Especie":null,Valor6=homologation?"1":null,Valor11="0",Valor12="0" }]);
}
