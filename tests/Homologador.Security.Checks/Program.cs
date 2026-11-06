using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Reflection;
using Homologador.AzureApi;
using Homologador.AzureApi.Controllers;
using Homologador.AzureApi.Security;
using Homologador.AzureApi.Services;
using Homologador.Core.DTOs;
using Homologador.Core.Services;
using Homologador.WinForms.ApiClient;
using Homologador.WinForms.Forms;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

var passed = 0;
string[] AllRoles = ["Conversor_Recepcion", "Conversor_Calidad", "Conversor_Produccion"];
string[] AllPermissions = ["GRM.CONVERSOR.RECEPCION", "GRM.CONVERSOR.CALIDAD", "GRM.CONVERSOR.PRODUCCION"];
await Check("API construye sin configuración RSA", async () =>
{
    await using var app = AzureApiHost.Build([], AzureApiHost.DefaultBaseUrl);
    Assert(app is not null);
});
await Check("Bearer ausente", async () =>
{
    var (context, mock, services) = Context(_ => throw new Exception("Unexpected GRM call"));
    using (services)
    {
        Assert(!(await context.AuthenticateAsync()).Succeeded);
        await context.ChallengeAsync();
        Assert(context.Response.StatusCode == 401 && mock.Calls == 0);
    }
});
foreach (var scenario in new[] { "inválido", "expirado" })
    await Check($"Bearer {scenario}", async () =>
    {
        var (context, mock, services) = Context(_ => new(HttpStatusCode.Unauthorized), "bad-token");
        using (services)
        {
            Assert(!(await context.AuthenticateAsync()).Succeeded);
            await context.ChallengeAsync();
            Assert(context.Response.StatusCode == 401 && mock.Calls == 1);
        }
    });
await Check("/user válido y una consulta por solicitud", async () =>
{
    var (context, mock, services) = Context(_ => JsonUser(AllRoles, AllPermissions), "token");
    using (services)
    {
        var auth = await context.AuthenticateAsync();
        Assert(auth.Succeeded && auth.Principal!.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value == "7");
        var access = await services.GetRequiredService<GrmProfileAccess>().GetAsync(context, default);
        Assert(access.HasArea("Recepción") && access.HasArea("Calidad") && access.HasArea("Producción"));
        Assert(mock.Calls == 1);
        var denied = await services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(auth.Principal!, "HomologadorAdmin");
        Assert(!denied.Succeeded);
    }
});
await Check("/user deniega 403", async () =>
{
    var (context, mock, services) = Context(_ => new(HttpStatusCode.Forbidden), "token");
    using (services)
    {
        Assert(!(await context.AuthenticateAsync()).Succeeded);
        await context.ChallengeAsync();
        Assert(context.Response.StatusCode == 403 && mock.Calls == 1);
    }
});
await Check("GRM sin conexión deniega 503", async () =>
{
    var (context, mock, services) = Context(_ => throw new HttpRequestException("offline"), "token");
    using (services)
    {
        Assert(!(await context.AuthenticateAsync()).Succeeded);
        await context.ChallengeAsync();
        Assert(context.Response.StatusCode == 503 && mock.Calls == 1);
    }
});
await Check("un perfil no autoriza otras áreas", async () =>
{
    var (context, _, services) = Context(_ => JsonUser(["Conversor_Calidad"], ["GRM.CONVERSOR.CALIDAD"]), "token");
    using (services)
    {
        Assert((await context.AuthenticateAsync()).Succeeded);
        var result = await services.GetRequiredService<GrmProfileAccess>().GetAsync(context, default);
        Assert(result.HasArea("Calidad") && !result.HasArea("Recepción") && !result.HasArea("Producción"));
    }
});
await Check("perfil sin permiso deniega área", async () =>
{
    var (context, _, services) = Context(_ => JsonUser(AllRoles, ["GRM.CONVERSOR.CALIDAD"]), "token");
    using (services)
    {
        Assert((await context.AuthenticateAsync()).Succeeded);
        var result = await services.GetRequiredService<GrmProfileAccess>().GetAsync(context, default);
        Assert(result.HasArea("Calidad") && !result.HasArea("Recepción") && !result.HasArea("Producción"));
    }
});
await Check("respuesta de usuario incompleta deniega", async () =>
{
    var (context, _, services) = Context(_ => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { id = 0, email = "test@example.org", username = "test",
            name = "Test", lastName = "User", company = new { id = 1 }, roles = AllRoles, permissions = AllPermissions })
    }, "token");
    using (services)
        Assert(!(await context.AuthenticateAsync()).Succeeded);
});
await Check("renovación antes de expirar y cierre de sesión", async () =>
{
    var logins = 0;
    using var http = new HttpClient(new MockHandler(request =>
    {
        Assert(request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/auth/login");
        var number = Interlocked.Increment(ref logins);
        var seconds = number == 1 ? 30 : 3600;
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(expires: DateTime.UtcNow.AddSeconds(seconds)));
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accessToken = token, expiresIn = seconds, tokenType = "Bearer" })
        };
    }));
    using var manager = new GrmTokenManager(http);
    await manager.LoginAsync("test", "example");
    var renewed = await manager.GetTokenAsync();
    Assert(!string.IsNullOrWhiteSpace(renewed) && logins == 2);
    Assert(await manager.GetTokenAsync() == renewed && logins == 2);
    manager.Logout();
    try { await manager.GetTokenAsync(); throw new Exception("Logout did not clear token"); }
    catch (InvalidOperationException) { }
});
await Check("Recepción lee GRM con JWT y conserva NULL/ReportId", async () =>
{
    var (client, grm) = await ReceptionClient(request =>
    {
        Assert(request.Method == HttpMethod.Get);
        Assert(request.RequestUri!.AbsolutePath == "/api/conversor/persistencias");
        Assert(request.RequestUri.Query == "?tipoProceso=1");
        Assert(request.Headers.Authorization?.Scheme == "Bearer");
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { tipoProceso = 1, registros = new object[]
            {
                new { lote = "TEST-REC-001", reportId = (int?)null,
                    fechaHoraCreacionArchivo = (DateTime?)null, productor = (string?)null,
                    kg = (decimal?)null, estado = (string?)null },
                new { lote = "R-NEW", reportId = (int?)7,
                    fechaHoraCreacionArchivo = (DateTime?)DateTime.UtcNow,
                    productor = (string?)"P", kg = (decimal?)100m, estado = (string?)"Cargado" }
            } })
        };
    });
    using (client)
    {
        var records = await client.GetRecepcionPersistenciasAsync();
        Assert(records.Count == 2 && records[0].ReportId is null
            && records[0].FechaHoraCreacionArchivo is null
            && records[0].Productor is null && records[0].Kg is null);
        Assert(records.Count(r => r.ReportId == 7) == 1);
        Assert(records.Select(r => r.Lote).Contains("TEST-REC-001"));
        Assert(grm.Calls == 1);
    }
});
await Check("Recepción distingue GET vacío de error", async () =>
{
    var (client, _) = await ReceptionClient(_ => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { tipoProceso = 1, registros = Array.Empty<object>() })
    });
    using (client) Assert((await client.GetRecepcionPersistenciasAsync()).Count == 0);

    var (failed, _) = await ReceptionClient(_ => new(HttpStatusCode.InternalServerError));
    using (failed)
    {
        try { await failed.GetRecepcionPersistenciasAsync(); throw new Exception("GET failure was hidden"); }
        catch (HttpRequestException) { }
    }
});
await Check("Recepción POST envía metadata y no confirma errores", async () =>
{
    var fileTime = new DateTime(2026, 11, 3, 14, 0, 0, DateTimeKind.Utc);
    var (client, grm) = await ReceptionClient(request =>
    {
        Assert(request.Method == HttpMethod.Post);
        Assert(request.RequestUri!.AbsolutePath == "/api/conversor/persistencias");
        Assert(request.Headers.Authorization?.Scheme == "Bearer");
        using var payload = System.Text.Json.JsonDocument.Parse(
            request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        var root = payload.RootElement;
        Assert(root.GetProperty("tipoProceso").GetInt32() == 1);
        var row = root.GetProperty("registros")[0];
        Assert(row.GetProperty("lote").GetString() == "R-NEW"
            && row.GetProperty("reportId").GetInt32() == 7
            && row.GetProperty("productor").GetString() == "P"
            && row.GetProperty("kg").GetDecimal() == 100m);
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { insertados = new[] { "R-NEW" }, existentes = Array.Empty<string>() })
        };
    });
    using (client)
    {
        var result = await client.RegisterRecepcionAsync(
            [new("R-NEW", 7, fileTime, "P", 100m)]);
        Assert(result.Insertados.SequenceEqual(["R-NEW"]) && grm.Calls == 1);
    }

    var (failed, _) = await ReceptionClient(_ => new(HttpStatusCode.BadRequest));
    using (failed)
    {
        try
        {
            await failed.RegisterRecepcionAsync([new("R-NEW", 7, fileTime, "P", 100m)]);
            throw new Exception("POST failure was hidden");
        }
        catch (HttpRequestException) { }
    }
});
await Check("Calidad consulta GRM y distingue lista vacía de error", async () =>
{
    var (client, _) = await ReceptionClient(request =>
    {
        Assert(request.Method == HttpMethod.Get && request.RequestUri!.Query == "?tipoProceso=2");
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { tipoProceso = 2,
            registros = new[] { new { lote = "C-1", reportId = (int?)7,
                fechaHoraCreacionArchivo = (DateTime?)null, productor = (string?)null } } }) };
    });
    using (client)
    {
        var records = await client.GetPersistenciasAsync(2);
        Assert(records.Count == 1 && records[0].Lote == "C-1" && records[0].ReportId == 7);
    }
    var (empty, _) = await ReceptionClient(_ => new(HttpStatusCode.OK) {
        Content = JsonContent.Create(new { tipoProceso = 2, registros = Array.Empty<object>() }) });
    using (empty) Assert((await empty.GetPersistenciasAsync(2)).Count == 0);
    var (failed, _) = await ReceptionClient(_ => new(HttpStatusCode.InternalServerError));
    using (failed)
    {
        try { await failed.GetPersistenciasAsync(2); throw new Exception("GET failure was hidden"); }
        catch (HttpRequestException) { }
    }
});
await Check("Producción consulta GRM con ambos pallets y ReportId", async () =>
{
    var (client, _) = await ReceptionClient(request =>
    {
        Assert(request.Method == HttpMethod.Get && request.RequestUri!.Query == "?tipoProceso=3");
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { tipoProceso = 3,
            registros = new[] {
                new { lote = "LP-1", reportId = (int?)7, codigoPallet = "PAL-A" },
                new { lote = "LP-1", reportId = (int?)7, codigoPallet = "PAL-B" },
                new { lote = "LP-1", reportId = (int?)8, codigoPallet = "PAL-A" },
                new { lote = "LP-1", reportId = (int?)null, codigoPallet = "PAL-A" } } }) };
    });
    using (client)
    {
        var records = await client.GetPersistenciasAsync(3);
        Assert(records.Count == 4 && records.Count(r => r.ReportId == 7) == 2);
        Assert(records.Select(r => (r.ReportId, r.Lote, r.CodigoPallet)).Distinct().Count() == 4);
    }
});
await Check("Calidad envía POST estructurado con Productor opcional", async () =>
{
    var date = DateTime.UtcNow;
    var (client, _) = await ReceptionClient(request =>
    {
        using var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        var root = payload.RootElement;
        var row = root.GetProperty("registros")[0];
        Assert(request.Method == HttpMethod.Post && root.GetProperty("tipoProceso").GetInt32() == 2);
        Assert(row.GetProperty("lote").GetString() == "C-1"
            && row.GetProperty("reportId").GetInt32() == 7
            && row.GetProperty("productor").ValueKind == JsonValueKind.Null);
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new {
            insertados = new[] { "C-1" }, existentes = Array.Empty<string>() }) };
    });
    using (client)
        Assert((await client.RegisterCalidadAsync([new("C-1", 7, date, null)]))
            .Insertados.SequenceEqual(["C-1"]));
});
await Check("Producción envía identidad completa y lee confirmaciones tipadas", async () =>
{
    var date = DateTime.UtcNow;
    var (client, _) = await ReceptionClient(request =>
    {
        using var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        var root = payload.RootElement;
        var row = root.GetProperty("registros")[0];
        Assert(request.Method == HttpMethod.Post && root.GetProperty("tipoProceso").GetInt32() == 3);
        Assert(row.GetProperty("lote").GetString() == "LP-1"
            && row.GetProperty("codigoPallet").GetString() == "PAL-A"
            && row.GetProperty("reportId").GetInt32() == 7
            && row.GetProperty("calibre").GetString() == "36");
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new {
            insertados = Array.Empty<string>(), existentes = Array.Empty<string>(),
            insertadosRegistros = new[] { new { reportId = 7, lote = "LP-1", codigoPallet = "PAL-A" } },
            existentesRegistros = Array.Empty<object>() }) };
    });
    using (client)
    {
        var result = await client.RegisterProduccionAsync([new("LP-1", 7, "PAL-A", date, null, "36")]);
        Assert(result.InsertadosRegistros?.Single().CodigoPallet == "PAL-A"
            && result.ExistentesRegistros?.Count == 0);
    }
    var (failed, _) = await ReceptionClient(_ => new(HttpStatusCode.BadRequest));
    using (failed)
    {
        try { await failed.RegisterProduccionAsync([new("LP-1", 7, "PAL-A", date, null, null)]);
            throw new Exception("POST failure was hidden"); }
        catch (HttpRequestException) { }
    }
});
await Check("Identidades de grilla respetan lote y reporte/pallet", () =>
{
    var grmKeys = typeof(MainForm).GetMethod("GrmKeys", BindingFlags.NonPublic | BindingFlags.Static)!;
    var records = new GrmRecepcionRecord[]
    {
        new("LP-1", 7, null, null, null, null, "PAL-A"),
        new("LP-1", 7, null, null, null, null, "PAL-B"),
        new("LP-1", 8, null, null, null, null, "PAL-A"),
        new("LP-2", null, null, null, null, null, "PAL-C")
    };
    var quality = (HashSet<string>)grmKeys.Invoke(null, [records, 2, 7])!;
    Assert(quality.SetEquals(["LP-1", "LP-2"]));
    var production = (HashSet<string>)grmKeys.Invoke(null, [records, 3, 7])!;
    Assert(production.SetEquals(["LP-1|PAL-A", "LP-1|PAL-B"]));

    var split = typeof(MainForm).GetMethod("TrySplitProductionKey",
        BindingFlags.NonPublic | BindingFlags.Static)!;
    var valid = new object?[] { "LP-1|PAL-A", null, null };
    Assert((bool)split.Invoke(null, valid)! && (string)valid[1]! == "LP-1"
        && (string)valid[2]! == "PAL-A");
    Assert(!(bool)split.Invoke(null, new object?[] { "LP-1", null, null })!);
    return Task.CompletedTask;
});
var equivalenceRows = new List<GrmEquivalenceRow>
{
    new(2, "Cereza", "CEREZAS"), new(2, "Ciruela", "CIRUELAS"),
    new(3, "no", "No Cumple"), new(3, "si", "Cumple"),
    new(4, "no", "Cumple"), new(4, "si", "No Cumple"),
    new(5, "Si", "Hidrocooler")
};
foreach (var (reportId, lookupIds) in new[]
{
    (2, new[] { 1 }), (1, new[] { 2, 3, 4 }), (3, new[] { 1 })
})
    await Check($"GRM equivalencias reporte {reportId}", async () =>
    {
        var context = EquivalenceContext(reportId, lookupIds);
        var (client, local, grm) = await EquivalenceClient(context, _ =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(equivalenceRows) });
        using (client)
        {
            var loaded = await client.GetExecutionContextAsync(reportId);
            Assert(local.Calls == 1 && grm.Calls == 2);
            Assert(loaded.Lookups.Select(x => x.Id).SequenceEqual(lookupIds));
            Assert(loaded.Lookups.All(x => x.Entries.Count > 0));
            var source = new Dictionary<string, object?>();
            foreach (var (lookupId, value) in new[] { (1, " CEREZA "), (2, " NO "), (3, " si "), (4, " si ") })
                source[$"S{lookupId}"] = value;
            var original = loaded.Lookups.Select(x => x with
            {
                Entries = equivalenceRows.Where(row => row.IdParametro == x.Id + 1)
                    .Select((row, index) => new LookupEntryDto(index + 1, row.Parametro, row.Valor, null))
                    .ToList()
            }).ToList();
            var engine = new TransformationEngine();
            var before = engine.Transform(context.Rules, original, [source]);
            var after = engine.Transform(loaded.Rules, loaded.Lookups, [source]);
            Assert(JsonSerializer.Serialize(before.Rows) == JsonSerializer.Serialize(after.Rows));
            Assert(after.WarningCount == 0);
            var generator = new FileGeneratorService();
            var originalCsv = generator.GenerateFile(before.Columns, before.Rows, "CSV", null, "test.csv");
            var grmCsv = generator.GenerateFile(after.Columns, after.Rows, "CSV", null, "test.csv");
            Assert(originalCsv.FileBytes.SequenceEqual(grmCsv.FileBytes));
        }
    });
await Check("Seven GRM pairs preserve exact mapped values", () =>
{
    var engine = new TransformationEngine();
    foreach (var row in equivalenceRows)
    {
        var lookupId = row.IdParametro - 1;
        var context = EquivalenceContext(1, [lookupId]);
        var mapped = GrmEquivalenceMapper.WithLookups(context, equivalenceRows);
        var result = engine.Transform(mapped.Rules, mapped.Lookups,
            [new Dictionary<string, object?> { [$"S{lookupId}"] = $" {row.Parametro.ToUpperInvariant()} " }]);
        Assert((string?)result.Rows.Single()[$"C{lookupId}"] == row.Valor);
        Assert(result.WarningCount == 0);
    }
    return Task.CompletedTask;
});
await Check("Unknown individual key keeps V1 warning and source value", () =>
{
    var context = EquivalenceContext(2, [1]);
    var mapped = GrmEquivalenceMapper.WithLookups(context, equivalenceRows);
    var result = new TransformationEngine().Transform(mapped.Rules, mapped.Lookups,
        [new Dictionary<string, object?> { ["S1"] = " Desconocida " }]);
    Assert((string?)result.Rows.Single()["C1"] == "Desconocida");
    Assert(result.WarningCount == 1);
    return Task.CompletedTask;
});
await Check("GRM unavailable blocks context", async () =>
{
    var (client, _, _) = await EquivalenceClient(EquivalenceContext(1, [2]), _ =>
        new(HttpStatusCode.ServiceUnavailable));
    using (client)
        await MustThrowAsync(() => client.GetExecutionContextAsync(1));
});
await Check("Required GRM group missing blocks context", async () =>
{
    var (client, _, _) = await EquivalenceClient(EquivalenceContext(1, [2]), _ =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(equivalenceRows.Where(x => x.IdParametro != 3)) });
    using (client)
        await MustThrowAsync(() => client.GetExecutionContextAsync(1));
});
await Check("Normalized duplicate blocks context", async () =>
{
    var duplicated = equivalenceRows.Append(new GrmEquivalenceRow(2, " cereza ", "OTRO")).ToList();
    var (client, _, _) = await EquivalenceClient(EquivalenceContext(2, [1]), _ =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(duplicated) });
    using (client)
        await MustThrowAsync(() => client.GetExecutionContextAsync(2));
});
await Check("Equivalences and persistence share Azure host, client and JWT", async () =>
{
    var requests = new List<(HttpMethod Method, Uri Url, string? Token)>();
    var (client, local, grm) = await EquivalenceClient(EquivalenceContext(2, [1]), request =>
    {
        requests.Add((request.Method, request.RequestUri!, request.Headers.Authorization?.Parameter));
        return (request.Method, request.RequestUri!.AbsolutePath) switch
        {
            (var method, "/api/conversor/reglas") when method == HttpMethod.Get =>
                new(HttpStatusCode.OK) { Content = JsonContent.Create(new GrmRulesResponse(1,
                    [new GrmRuleRow
                    {
                        IdEquivalencia = 1001, IdAplicacion = 2, TipoProceso = 1,
                        Valor1 = "1", Valor2 = "C1", Valor3 = "Homologation",
                        Valor4 = "S1", Valor6 = "1", Valor11 = "0", Valor12 = "0"
                    }])) },
            (var method, "/api/conversor/equivalencias") when method == HttpMethod.Get =>
                new(HttpStatusCode.OK) { Content = JsonContent.Create(equivalenceRows) },
            (var method, "/api/conversor/persistencias") when method == HttpMethod.Get =>
                new(HttpStatusCode.OK) { Content = JsonContent.Create(
                    new GrmRecepcionResponse(1, [])) },
            (var method, "/api/conversor/persistencias") when method == HttpMethod.Post =>
                new(HttpStatusCode.OK) { Content = JsonContent.Create(
                    new GrmRecepcionRegistrationResponse([], [])) },
            _ => throw new Exception("Unexpected GRM route")
        };
    }, expectedPath: null);
    using (client)
    {
        Assert((await client.GetExecutionContextAsync(2)).Lookups.Count == 1);
        Assert((await client.GetPersistenciasAsync(1)).Count == 0);
        await client.RegisterRecepcionAsync([]);
    }
    var azure = new Uri(AzureApiHost.GrmCalidadBaseUrl);
    Assert(local.Calls == 1 && grm.Calls == 4 && requests.Count == 4);
    Assert(requests.All(x => x.Url.Scheme == azure.Scheme
        && x.Url.Authority == azure.Authority && !string.IsNullOrWhiteSpace(x.Token)));
    Assert(requests.Select(x => x.Token).Distinct(StringComparer.Ordinal).Count() == 1);
});
await Check("V3 report, pipeline and process IDs remain independent", () =>
{
    Assert(ReportProcessMap.ForReport(1).MappingPipelineId == 1
        && ReportProcessMap.ForReport(1).TipoProceso == 2);
    Assert(ReportProcessMap.ForReport(2).MappingPipelineId == 3
        && ReportProcessMap.ForReport(2).TipoProceso == 1);
    Assert(ReportProcessMap.ForReport(3).MappingPipelineId == 2
        && ReportProcessMap.ForReport(3).TipoProceso == 3);
    Assert(ReportProcessMap.ForPipeline(2).ReportId == 3);
    MustReject(() => ReportProcessMap.ForReport(4));
    return Task.CompletedTask;
});
await Check("Real pipeline controller uses the V2 GRM host and forwards the incoming JWT without SQL", async () =>
{
    await using var app = AzureApiHost.Build([], AzureApiHost.DefaultBaseUrl);
    using var configured = app.Services.GetRequiredService<IHttpClientFactory>()
        .CreateClient(nameof(GrmRuleReader));
    Assert(configured.BaseAddress == new Uri(AzureApiHost.GrmCalidadBaseUrl));
    var requested = new List<int>();
    var mock = new MockHandler(request =>
    {
        Assert(request.Method == HttpMethod.Get
            && request.RequestUri!.GetLeftPart(UriPartial.Authority)
                == configured.BaseAddress!.GetLeftPart(UriPartial.Authority)
            && request.RequestUri.AbsolutePath == "/api/conversor/reglas"
            && request.Headers.Authorization?.ToString() == "Bearer incoming-jwt");
        var process = int.Parse(request.RequestUri!.Query[^1..]);
        requested.Add(process);
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new GrmRulesResponse(process, GrmRuleRows(process)))
        };
    });
    using var http = new HttpClient(mock) { BaseAddress = configured.BaseAddress };
    // Null database contexts ensure this action cannot silently read either local table.
    var controller = new AdminController(null!, new GrmRuleReader(http))
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };
    controller.Request.Headers.Authorization = "Bearer incoming-jwt";
    var action = await controller.GetPipelines(default);
    var pipelines = (IReadOnlyList<AdminPipelineDto>)((OkObjectResult)action.Result!).Value!;
    Assert(requested.SequenceEqual([2, 3, 1]) && mock.Calls == 3);
    Assert(pipelines.Select(p => p.RuleCount).SequenceEqual([80, 19, 23]));
    Assert(pipelines.All(p => p.Rules.Count == p.RuleCount
        && p.Rules.Select(r => r.SortOrder).SequenceEqual(Enumerable.Range(1, p.RuleCount))));
});
await Check("Real pipeline controller stops on GRM failure without SQL fallback", async () =>
{
    foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
        HttpStatusCode.ServiceUnavailable })
    {
        var mock = new MockHandler(_ => new(status));
        using var http = new HttpClient(mock) { BaseAddress = new Uri(AzureApiHost.GrmCalidadBaseUrl) };
        var controller = new AdminController(null!, new GrmRuleReader(http))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.Request.Headers.Authorization = "Bearer incoming-jwt";
        var action = await controller.GetPipelines(default);
        Assert(((ObjectResult)action.Result!).StatusCode == (int)status && mock.Calls == 1);
    }
});
await Check("V3 mapping preserves single and composite row identities", () =>
{
    foreach (var process in ReportProcessMap.All)
    {
        var rows = GrmRuleRows(process.TipoProceso);
        var index = rows.FindIndex(r => r.Valor1 == "1");
        rows[index] = rows[index] with { Valor2 = "ID_LOTE", Valor12 = "1" };
        var output = new Dictionary<string, object?> { ["ID_LOTE"] = " LOTE-Ñ " };
        var expected = "LOTE-Ñ";
        if (process.TipoProceso == 3)
        {
            index = rows.FindIndex(r => r.Valor1 == "2");
            rows[index] = rows[index] with { Valor2 = "ID_PALLET", Valor12 = "1" };
            output["ID_PALLET"] = " PAL-Á ";
            expected += "|PAL-Á";
        }
        var rules = GrmRuleMapper.MapRules(process.ReportId,
            new GrmRulesResponse(process.TipoProceso, rows));
        var area = process.TipoProceso == 3 ? "Producción" : process.TipoProceso == 2 ? "Calidad" : "Recepción";
        Assert(RowIdentityExtractor.ExtractPrimaryKey(area, rules, output) == expected);
    }
    return Task.CompletedTask;
});
await Check("V3 is active in the existing execution path", async () =>
{
    var rows = GrmRuleRows(2);
    var (client, local, grm) = await EquivalenceClient(EquivalenceContext(1, []), request =>
    {
        Assert(request.RequestUri!.AbsolutePath == "/api/conversor/reglas");
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new GrmRulesResponse(2, rows)) };
    }, expectedPath: "/api/conversor/reglas");
    using (client)
    {
        var context = await client.GetExecutionContextAsync(1);
        Assert(context.Rules.Count == 80 && local.Calls == 1 && grm.Calls == 1);
    }
});
await Check("GRM reader accepts changed rule count without a fixed limit", async () =>
{
    var mock = new MockHandler(request =>
    {
        Assert(request.RequestUri!.PathAndQuery == "/api/conversor/reglas?tipoProceso=2");
        var rows = GrmRuleRows(2);
        rows.Add(rows[0] with { IdEquivalencia = 9999, Valor1 = "81", Valor2 = "C81" });
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new GrmRulesResponse(2, rows)) };
    });
    using var http = new HttpClient(mock) { BaseAddress = new Uri("https://grm.example/") };
    var rules = await new GrmRuleReader(http).GetRulesAsync(1, "Bearer test-token", default);
    Assert(rules.Count == 81 && rules[^1].SortOrder == 81 && mock.Calls == 1);
});
await Check("API report and admin contracts retain GRM rule details", async () =>
{
    var reports = ReportProcessMap.All.Select(p => EquivalenceContext(p.ReportId, []).Report
        with { RuleCount = GrmRuleRows(p.TipoProceso).Count, PipelineName = p.Name }).ToList();
    var pipelines = ReportProcessMap.All.Select(p =>
    {
        var rules = GrmRuleMapper.MapRules(p.ReportId,
            new GrmRulesResponse(p.TipoProceso, GrmRuleRows(p.TipoProceso)));
        return new AdminPipelineDto(p.MappingPipelineId, p.Codigo, p.Name,
            p.Description, true, rules.Count, rules);
    }).ToList();
    var (client, local, grm) = await EquivalenceClient(EquivalenceContext(1, []),
        _ => throw new Exception("Unexpected direct GRM call"), expectedPath: null,
        localReply: request => request.RequestUri!.AbsolutePath switch
        {
            "/api/reports/my-reports" or "/api/admin/reports" =>
                new(HttpStatusCode.OK) { Content = JsonContent.Create(reports) },
            "/api/admin/pipelines" =>
                new(HttpStatusCode.OK) { Content = JsonContent.Create(pipelines) },
            _ => throw new Exception("Unexpected local route")
        });
    using (client)
    {
        Assert((await client.GetMyReportsAsync()).Select(r => r.RuleCount).SequenceEqual([80, 23, 19]));
        Assert((await client.GetAdminReportsAsync()).Select(r => r.RuleCount).SequenceEqual([80, 23, 19]));
        var loaded = await client.GetAdminPipelinesAsync();
        Assert(loaded.Single(p => p.Id == 1).Rules.Count == 80);
        Assert(loaded.Single(p => p.Id == 2).Rules.Count == 19);
        Assert(loaded.Single(p => p.Id == 3).Rules.Count == 23);
        Assert(local.Calls == 3 && grm.Calls == 0);
    }
});
await Check("Local API surfaces a clear GRM rules failure", async () =>
{
    var (client, local, grm) = await EquivalenceClient(EquivalenceContext(1, []),
        _ => throw new Exception("Unexpected direct GRM call"), expectedPath: null,
        localReply: _ => new(HttpStatusCode.ServiceUnavailable)
        {
            Content = JsonContent.Create(new { message = "No se pudo conectar con las reglas V3 de GRM." })
        });
    using (client)
    {
        try
        {
            await client.GetMyReportsAsync();
            throw new Exception("Expected failure was not raised.");
        }
        catch (HttpRequestException ex)
        {
            Assert(ex.Message.Contains("reglas V3 de GRM", StringComparison.Ordinal));
        }
        Assert(local.Calls == 1 && grm.Calls == 0);
    }
});
foreach (var (reportId, tipoProceso, count) in new[]
{
    (1, 2, 80), (2, 1, 23), (3, 3, 19)
})
    await Check($"V3 controlled GET for report {reportId}", async () =>
    {
        var rows = GrmRuleRows(tipoProceso);
        if (tipoProceso == 3)
        {
            var index = rows.FindIndex(row => row.Valor1 == "2");
            rows[index] = rows[index] with { Valor2 = "" };
        }
        var (client, local, grm) = await EquivalenceClient(EquivalenceContext(reportId, []), request =>
        {
            Assert(request.RequestUri!.AbsolutePath == "/api/conversor/reglas"
                && request.RequestUri.Query == $"?tipoProceso={tipoProceso}"
                && request.Headers.Authorization?.Scheme == "Bearer"
                && !string.IsNullOrWhiteSpace(request.Headers.Authorization.Parameter));
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new GrmRulesResponse(tipoProceso, rows))
            };
        }, expectedPath: null);
        using (client)
        {
            var context = await client.GetExecutionContextWithGrmRulesAsync(reportId);
            Assert(local.Calls == 1 && grm.Calls == 1);
            Assert(context.Rules.Count == count && context.Report.RuleCount == count);
            Assert(context.Rules.Select(rule => rule.SortOrder).SequenceEqual(Enumerable.Range(1, count)));
            Assert(context.Rules[0].Id == 1000 + tipoProceso * 100 + 1);
            if (tipoProceso == 3)
            {
                Assert(context.Rules[1].TargetColumn == "");
                var result = new TransformationEngine().Transform(context.Rules, [],
                    [new Dictionary<string, object?>()]);
                Assert(result.Rows.Single().ContainsKey(""));
            }
        }
    });
await Check("V3 Valor1 through Valor14 preserve nulls, empty strings and Unicode", () =>
{
    var context = EquivalenceContext(1, []);
    var rows = GrmRuleRows(2);
    var index = rows.FindIndex(row => row.Valor1 == "1");
    rows[index] = rows[index] with
    {
        Valor2 = "Residuos Químicos", Valor3 = "Direct", Valor4 = "",
        Valor5 = null, Valor6 = "4", Valor7 = "", Valor8 = null,
        Valor9 = "dd.MM.yyyy", Valor10 = "COM, GRA", Valor11 = "1",
        Valor12 = "1", Valor13 = "5", Valor14 = "4"
    };
    var result = GrmRuleMapper.WithRules(context, new GrmRulesResponse(2, rows));
    var rule = result.Rules[0];
    Assert(rule.SortOrder == 1 && rule.TargetColumn == "Residuos Químicos"
        && rule.RuleType == "Direct" && rule.SourceColumn == ""
        && rule.FixedValue is null && rule.LookupTableId == 4
        && rule.LookupKeyColumn == "" && rule.LookupValueColumn is null
        && rule.DateFormat == "dd.MM.yyyy" && rule.Validation == "COM, GRA"
        && rule.IsRequired && rule.IsIdentity && rule.SubstringStart == 5
        && rule.SubstringLength == 4);
    Assert(result.Rules[1].LookupTableId is null && result.Rules[1].SourceColumn is null);
    return Task.CompletedTask;
});
await Check("V3 rules keep seven transformations and V2 homologation", () =>
{
    var rows = GrmRuleRows(2);
    var replacements = new Dictionary<int, GrmRuleRow>
    {
        [1] = new() { Valor2 = "DIRECTO", Valor3 = "Direct", Valor4 = "DAÑO DE PAJARO", Valor5 = "IGNORAR" },
        [2] = new() { Valor2 = "FIJO", Valor3 = "Fixed", Valor5 = "Sí" },
        [3] = new() { Valor2 = "FORMATO", Valor3 = "Formatted", Valor4 = "Lote", Valor5 = "A51", Valor12 = "1", Valor13 = "5", Valor14 = "4" },
        [4] = new() { Valor2 = "COMPUTADO", Valor3 = "Computed", Valor4 = "SinDato", Valor5 = "Respaldo" },
        [5] = new() { Valor2 = "HOMO", Valor3 = "Homologation", Valor4 = "Especie", Valor6 = "1" },
        [6] = new() { Valor2 = "AGG", Valor3 = "Aggregate", Valor4 = "A,B", Valor5 = "AVG" },
        [7] = new() { Valor2 = "VACIO", Valor3 = "Empty" }
    };
    foreach (var (order, values) in replacements)
    {
        var index = rows.FindIndex(row => row.Valor1 == order.ToString());
        rows[index] = rows[index] with
        {
            Valor2 = values.Valor2, Valor3 = values.Valor3, Valor4 = values.Valor4,
            Valor5 = values.Valor5, Valor6 = values.Valor6,
            Valor12 = values.Valor12 ?? "0", Valor13 = values.Valor13,
            Valor14 = values.Valor14
        };
    }
    var context = GrmRuleMapper.WithRules(EquivalenceContext(1, []),
        new GrmRulesResponse(2, rows));
    context = GrmEquivalenceMapper.WithLookups(context, equivalenceRows);
    var source = new Dictionary<string, object?>
    {
        ["DAÑO DE PAJARO"] = "7", ["Lote"] = "12345678",
        ["Especie"] = "Cereza", ["A"] = 2m, ["B"] = 4m
    };
    var transformed = new TransformationEngine().Transform(context.Rules, context.Lookups, [source]);
    var output = transformed.Rows.Single();
    Assert((string?)output["DIRECTO"] == "7" && (string?)output["FIJO"] == "Sí"
        && (string?)output["FORMATO"] == "A515678"
        && (string?)output["COMPUTADO"] == "Respaldo"
        && (string?)output["HOMO"] == "CEREZAS"
        && (decimal)output["AGG"]! == 3m && (string?)output["VACIO"] == ""
        && context.Rules[2].IsIdentity && transformed.WarningCount == 0);
    return Task.CompletedTask;
});
await Check("V3 HTTP path composes with V2 equivalences", async () =>
{
    var rows = GrmRuleRows(2);
    var index = rows.FindIndex(row => row.Valor1 == "1");
    rows[index] = rows[index] with
    {
        Valor2 = "ESPECIE", Valor3 = "Homologation", Valor4 = "Especie", Valor6 = "1"
    };
    var paths = new List<string>();
    var (client, local, grm) = await EquivalenceClient(EquivalenceContext(1, []), request =>
    {
        paths.Add(request.RequestUri!.AbsolutePath);
        return request.RequestUri.AbsolutePath switch
        {
            "/api/conversor/reglas" => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new GrmRulesResponse(2, rows))
            },
            "/api/conversor/equivalencias" => new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(equivalenceRows)
            },
            _ => throw new Exception("Unexpected GRM route")
        };
    }, expectedPath: null);
    using (client)
    {
        var loaded = await client.GetExecutionContextWithGrmRulesAsync(1);
        Assert(local.Calls == 1 && grm.Calls == 2);
        Assert(paths.SequenceEqual(["/api/conversor/reglas", "/api/conversor/equivalencias"]));
        Assert(loaded.Lookups.Single().Id == 1);
        var output = new TransformationEngine().Transform(loaded.Rules, loaded.Lookups,
            [new Dictionary<string, object?> { ["Especie"] = "Cereza" }]);
        Assert((string?)output.Rows.Single()["ESPECIE"] == "CEREZAS");
    }
});
await Check("V3 rejects incomplete and contradictory rule data", () =>
{
    var context = EquivalenceContext(1, []);
    var baseRows = GrmRuleRows(2);
    MustReject(() => GrmRuleMapper.WithRules(context, new GrmRulesResponse(2, [])));
    MustReject(() => GrmRuleMapper.WithRules(context, new GrmRulesResponse(1, baseRows)));
    MustReject(() => GrmRuleMapper.WithRules(context, new GrmRulesResponse(2,
        baseRows.Where(row => row.Valor1 != "40").ToList())));
    foreach (var invalid in new GrmRuleRow[]
    {
        baseRows[0] with { Valor1 = "X" },
        baseRows[0] with { Valor1 = "2" },
        baseRows[0] with { Valor2 = null },
        baseRows[0] with { Valor2 = baseRows[1].Valor2 },
        baseRows[0] with { Valor3 = null },
        baseRows[0] with { Valor6 = "" },
        baseRows[0] with { Valor11 = null },
        baseRows[0] with { Valor12 = "true" },
        baseRows[0] with { Valor13 = "bad" },
        baseRows[0] with { IdAplicacion = 3 },
        baseRows[0] with { IdParametro = 1 },
        baseRows[0] with { TipoProceso = 1 },
        baseRows[0] with { IdEquivalencia = baseRows[1].IdEquivalencia }
    })
    {
        var changed = baseRows.ToList();
        changed[0] = invalid;
        MustReject(() => GrmRuleMapper.WithRules(context, new GrmRulesResponse(2, changed)));
    }
    return Task.CompletedTask;
});
foreach (var status in new[] { HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable,
    HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden })
    await Check($"V3 HTTP {status} blocks context without local fallback", async () =>
    {
        var (client, local, grm) = await EquivalenceClient(EquivalenceContext(2, []),
            _ => new HttpResponseMessage(status), expectedPath: "/api/conversor/reglas");
        using (client)
        {
            await MustThrowAsync(() => client.GetExecutionContextWithGrmRulesAsync(2));
            Assert(local.Calls == 1 && grm.Calls == 1);
        }
    });
await Check("V3 timeout blocks context without local fallback", async () =>
{
    var (client, local, grm) = await EquivalenceClient(EquivalenceContext(2, []),
        _ => throw new TaskCanceledException("timeout"), expectedPath: "/api/conversor/reglas");
    using (client)
    {
        await MustThrowAsync(() => client.GetExecutionContextAsync(2));
        Assert(local.Calls == 1 && grm.Calls == 1);
    }
});
await Check("V3 malformed JSON blocks context", async () =>
{
    var (client, _, grm) = await EquivalenceClient(EquivalenceContext(3, []),
        _ => new(HttpStatusCode.OK) { Content = new StringContent("{bad json") },
        expectedPath: "/api/conversor/reglas");
    using (client)
    {
        await MustThrowAsync(() => client.GetExecutionContextWithGrmRulesAsync(3));
        Assert(grm.Calls == 1);
    }
});
await Check("V3 empty response blocks context", async () =>
{
    var (client, _, grm) = await EquivalenceClient(EquivalenceContext(1, []),
        _ => new(HttpStatusCode.OK) { Content = new StringContent("null") },
        expectedPath: "/api/conversor/reglas");
    using (client)
    {
        await MustThrowAsync(() => client.GetExecutionContextWithGrmRulesAsync(1));
        Assert(grm.Calls == 1);
    }
});
passed += await V4Checks.RunAsync();
Console.WriteLine($"{passed} security, equivalence, rule, and configuration checks passed.");

async Task Check(string name, Func<Task> action)
{
    await action();
    passed++;
    Console.WriteLine($"PASS {name}");
}

static void Assert(bool condition)
{
    if (!condition) throw new Exception("Security assertion failed.");
}

static async Task MustThrowAsync(Func<Task> action)
{
    try { await action(); }
    catch (HttpRequestException) { return; }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected failure was not raised.");
}

static void MustReject(Action action)
{
    try { action(); }
    catch (InvalidOperationException) { return; }
    throw new Exception("Expected invalid rules were accepted.");
}

static List<GrmRuleRow> GrmRuleRows(int tipoProceso)
{
    var count = tipoProceso switch { 1 => 23, 2 => 80, 3 => 19, _ => throw new Exception() };
    return Enumerable.Range(1, count).Reverse().Select(order => new GrmRuleRow
    {
        IdEquivalencia = 1000 + tipoProceso * 100 + order,
        IdAplicacion = 2,
        TipoProceso = tipoProceso,
        Valor1 = order.ToString(),
        Valor2 = $"C{order}",
        Valor3 = "Empty",
        Valor11 = "0",
        Valor12 = "0"
    }).ToList();
}

static ExecutionContextDto EquivalenceContext(int reportId, int[] lookupIds)
{
    var rules = lookupIds.Select((id, index) => new PipelineRuleDto(
        id, index + 1, $"C{id}", "Homologation", $"S{id}", null, id,
        null, null, null, null, false, false, null, null)).ToList();
    return new ExecutionContextDto(
        new ReportSummaryDto(reportId, "TEST", "Test", null, "Calidad", "Q", "P",
            rules.Count, "CSV", null, "test.csv"),
        new DataSourceConnectDto(1, "DS", "DS", "local", "db", 1, null, null, 30),
        new QueryExecutionDto(1, "Q", "Q", 1, "SELECT 1", []),
        rules, [], []);
}

static async Task<(HomologadorApiClient Client, MockHandler Local, MockHandler Grm)>
    EquivalenceClient(ExecutionContextDto context,
        Func<HttpRequestMessage, HttpResponseMessage> grmReply,
        string? expectedPath = "/api/conversor/equivalencias",
        Func<HttpRequestMessage, HttpResponseMessage>? localReply = null)
{
    var auth = new HttpClient(new MockHandler(_ =>
    {
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(1)));
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accessToken = token, expiresIn = 3600, tokenType = "Bearer" })
        };
    }));
    var tokens = new GrmTokenManager(auth);
    await tokens.LoginAsync("test", "example");
    var grm = new MockHandler(request =>
    {
        if (expectedPath == "/api/conversor/equivalencias"
            && request.RequestUri!.AbsolutePath == "/api/conversor/reglas")
        {
            var process = ReportProcessMap.ForReport(context.Report.Id);
            var rows = context.Rules.Select(rule => new GrmRuleRow
            {
                IdEquivalencia = 1000 + rule.Id,
                IdAplicacion = 2,
                TipoProceso = process.TipoProceso,
                Valor1 = rule.SortOrder.ToString(),
                Valor2 = rule.TargetColumn,
                Valor3 = rule.RuleType,
                Valor4 = rule.SourceColumn,
                Valor5 = rule.FixedValue,
                Valor6 = rule.LookupTableId?.ToString(),
                Valor7 = rule.LookupKeyColumn,
                Valor8 = rule.LookupValueColumn,
                Valor9 = rule.DateFormat,
                Valor10 = rule.Validation,
                Valor11 = rule.IsRequired ? "1" : "0",
                Valor12 = rule.IsIdentity ? "1" : "0",
                Valor13 = rule.SubstringStart?.ToString(),
                Valor14 = rule.SubstringLength?.ToString()
            }).ToList();
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new GrmRulesResponse(process.TipoProceso, rows))
            };
        }
        Assert(request.Method == HttpMethod.Get
            || (expectedPath is null && request.Method == HttpMethod.Post));
        Assert((expectedPath is null || request.RequestUri!.AbsolutePath == expectedPath)
            && request.Headers.Authorization?.Scheme == "Bearer");
        return grmReply(request);
    });
    var local = new MockHandler(request =>
    {
        if (localReply is not null) return localReply(request);
        Assert(request.RequestUri!.AbsolutePath == $"/api/reports/{context.Report.Id}/execution-context");
        using var rulesHttp = new HttpClient(grm, disposeHandler: false)
            { BaseAddress = new Uri(AzureApiHost.GrmCalidadBaseUrl) };
        IReadOnlyList<PipelineRuleDto> rules;
        try
        {
            rules = new GrmRuleReader(rulesHttp)
                .GetRulesAsync(context.Report.Id, request.Headers.Authorization!.ToString(), default)
                .GetAwaiter().GetResult();
        }
        catch (GrmRuleFetchException ex)
        {
            return new((HttpStatusCode)ex.StatusCode);
        }
        var loaded = context with
        {
            Report = context.Report with { RuleCount = rules.Count },
            Rules = rules,
            Lookups = []
        };
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(loaded) };
    });
    return (new HomologadorApiClient(new HttpClient(local), new HttpClient(grm),
        tokens, "http://localhost:5080"), local, grm);
}

static (DefaultHttpContext Context, MockHandler Mock, ServiceProvider Services) Context(
    Func<HttpRequestMessage, HttpResponseMessage> reply, string? bearer = null)
{
    var mock = new MockHandler(request =>
    {
        Assert(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/user");
        Assert(request.RequestUri!.Scheme == "https" && request.Headers.Authorization?.Parameter == bearer);
        return reply(request);
    });
    var http = new HttpClient(mock) { BaseAddress = new Uri("https://grm.example/") };
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddSingleton(new GrmProfileAccess(http));
    services.AddAuthentication(GrmAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, GrmAuthenticationHandler>(GrmAuthenticationHandler.SchemeName, _ => { });
    services.AddAuthorization(options => options.AddPolicy("HomologadorAdmin", p => p.RequireAssertion(_ => false)));
    var provider = services.BuildServiceProvider();
    var context = new DefaultHttpContext { RequestServices = provider };
    if (bearer is not null) context.Request.Headers.Authorization = $"Bearer {bearer}";
    return (context, mock, provider);
}

static HttpResponseMessage JsonUser(string[] roles, string[] permissions) => new(HttpStatusCode.OK)
{
    Content = JsonContent.Create(new { id = 7, email = "test@example.org", username = "test",
        name = "Test", lastName = "User", company = new { id = 5 }, roles, permissions })
};

static async Task<(HomologadorApiClient Client, MockHandler Grm)> ReceptionClient(
    Func<HttpRequestMessage, HttpResponseMessage> reply)
{
    var auth = new HttpClient(new MockHandler(_ =>
    {
        var token = new JwtSecurityTokenHandler().WriteToken(
            new JwtSecurityToken(expires: DateTime.UtcNow.AddHours(1)));
        return new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { accessToken = token, expiresIn = 3600, tokenType = "Bearer" })
        };
    }));
    var tokens = new GrmTokenManager(auth);
    await tokens.LoginAsync("test", "example");
    var grm = new MockHandler(reply);
    var client = new HomologadorApiClient(
        new HttpClient(new MockHandler(_ => throw new Exception("Unexpected local call"))),
        new HttpClient(grm), tokens, "http://localhost:5080");
    return (client, grm);
}

sealed class MockHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(reply(request));
    }
}
