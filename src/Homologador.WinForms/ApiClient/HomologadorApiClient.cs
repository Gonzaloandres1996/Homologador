using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Homologador.AzureApi;
using Homologador.Core.DTOs;
using Homologador.Core.Services;

namespace Homologador.WinForms.ApiClient;

public sealed class HomologadorApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly HttpClient _grmCalidadHttp;
    private readonly GrmTokenManager _tokens;
    public UserSessionDto? CurrentUser { get; private set; }

    public HomologadorApiClient(string baseUrl) : this(
        new HttpClient(), new HttpClient(), new GrmTokenManager(), baseUrl) { }

    public HomologadorApiClient(
        HttpClient localHttp, HttpClient grmCalidadHttp, GrmTokenManager tokens, string baseUrl)
    {
        _http = localHttp;
        _http.BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/");
        _http.Timeout = TimeSpan.FromSeconds(30);
        _grmCalidadHttp = grmCalidadHttp;
        _grmCalidadHttp.BaseAddress = new Uri(AzureApiHost.GrmCalidadBaseUrl);
        _grmCalidadHttp.Timeout = TimeSpan.FromSeconds(30);
        _tokens = tokens;
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = await _http.GetAsync("api/health", cts.Token);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("El puerto 5080 está ocupado por una API incompatible.");
            using var body = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cts.Token), cancellationToken: cts.Token);
            if (!body.RootElement.TryGetProperty("authentication", out var authentication)
                || authentication.GetString() != "GRM")
                throw new InvalidOperationException(
                    "Hay una API antigua en el puerto 5080. Ciérrela antes de iniciar el Homologador.");
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("El puerto 5080 está ocupado por una API incompatible.");
        }
    }

    public async Task<UserSessionDto> LoginAsync(string username, string password)
    {
        await _tokens.LoginAsync(username, password);
        try
        {
            CurrentUser = await GetAsync<UserSessionDto>("api/auth/me");
            return CurrentUser;
        }
        catch
        {
            Logout();
            throw;
        }
    }

    public void Logout()
    {
        _tokens.Logout();
        CurrentUser = null;
    }

    public Task<IReadOnlyList<ReportSummaryDto>> GetMyReportsAsync() =>
        GetListAsync<ReportSummaryDto>("api/reports/my-reports");

    public Task<ExecutionContextDto> GetExecutionContextAsync(int reportId) =>
        GetExecutionContextWithGrmRulesAsync(reportId);

    public Task<ExecutionContextDto> GetExecutionContextWithGrmRulesAsync(int reportId) =>
        GetExecutionContextCoreAsync(reportId);

    private async Task<ExecutionContextDto> GetExecutionContextCoreAsync(int reportId)
    {
        var context = await GetAsync<ExecutionContextDto>(
            $"api/reports/{reportId}/execution-context");
        if (context.Report.Id != reportId)
            throw new InvalidOperationException("La API local devolvio un reporte inesperado.");
        if (context.Rules.Count == 0 || context.Report.RuleCount != context.Rules.Count
            || !context.Rules.Select(rule => rule.SortOrder)
                .SequenceEqual(Enumerable.Range(1, context.Rules.Count)))
            throw new InvalidOperationException("La API local devolvio reglas V3 incompletas.");
        if (!context.Rules.Any(rule => rule.LookupTableId.HasValue))
            return context;

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/conversor/equivalencias");
        using var response = await SendAuthenticatedAsync(request, _grmCalidadHttp);
        var equivalences = await response.Content.ReadFromJsonAsync<List<GrmEquivalenceRow>>()
            ?? throw new InvalidOperationException("GRM devolvio equivalencias vacias o invalidas.");
        return GrmEquivalenceMapper.WithLookups(context, equivalences);
    }

    public Task<IReadOnlyList<GrmRecepcionRecord>> GetRecepcionPersistenciasAsync() =>
        GetPersistenciasAsync(1);

    public async Task<IReadOnlyList<GrmRecepcionRecord>> GetPersistenciasAsync(int tipoProceso)
    {
        if (tipoProceso is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(tipoProceso));
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/conversor/persistencias?tipoProceso={tipoProceso}");
        using var response = await SendAuthenticatedAsync(request, _grmCalidadHttp);
        var result = await response.Content.ReadFromJsonAsync<GrmRecepcionResponse>()
            ?? throw new InvalidOperationException("GRM devolvió una respuesta vacía.");
        if (result.TipoProceso != tipoProceso || result.Registros is null)
            throw new InvalidOperationException("GRM devolvió persistencias de un proceso inesperado.");
        return result.Registros;
    }

    public async Task<GrmRecepcionRegistrationResponse> RegisterRecepcionAsync(
        IReadOnlyList<GrmRecepcionRegistrationItem> registros)
        => await RegisterPersistenciasAsync(1, registros);

    public Task<GrmRecepcionRegistrationResponse> RegisterCalidadAsync(
        IReadOnlyList<GrmCalidadRegistrationItem> registros)
        => RegisterPersistenciasAsync(2, registros);

    public Task<GrmRecepcionRegistrationResponse> RegisterProduccionAsync(
        IReadOnlyList<GrmProduccionRegistrationItem> registros)
        => RegisterPersistenciasAsync(3, registros);

    private async Task<GrmRecepcionRegistrationResponse> RegisterPersistenciasAsync<T>(
        int tipoProceso, IReadOnlyList<T> registros)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "api/conversor/persistencias")
        {
            Content = JsonContent.Create(new { tipoProceso, registros })
        };
        using var response = await SendAuthenticatedAsync(request, _grmCalidadHttp);
        return await response.Content.ReadFromJsonAsync<GrmRecepcionRegistrationResponse>()
            ?? throw new InvalidOperationException("GRM devolvió una respuesta vacía.");
    }

    public Task<MarkGeneratedResponse> MarkGeneratedAsync(MarkGeneratedRequest request) =>
        PostAsync<MarkGeneratedResponse>("api/records/mark-generated", request);

    public async Task<IReadOnlyList<RecordGridRowDto>> GetRecordsByAreaAsync(
        string area, string? search = null, int? reportId = null)
    {
        var url = $"api/records/{Uri.EscapeDataString(area)}";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        if (reportId.HasValue)
            queryParams.Add($"reportId={reportId.Value}");
        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);
        return await GetListAsync<RecordGridRowDto>(url);
    }

    public Task<IReadOnlyList<RecordGridRowDto>> GetRecordsByReportAsync(
        int reportId, string? search = null)
    {
        var url = $"api/records/by-report/{reportId}";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"?search={Uri.EscapeDataString(search)}";
        return GetListAsync<RecordGridRowDto>(url);
    }

    public Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync() =>
        GetListAsync<AdminUserDto>("api/admin/users");

    public Task AssignUserReportsAsync(int userId, IReadOnlyList<int> reportIds) =>
        PostWithoutResultAsync("api/admin/users/assign-reports",
            new AssignUserReportsRequest(userId, reportIds));

    public Task ResetPasswordAsync(int userId, string newPassword) =>
        PostWithoutResultAsync("api/admin/users/reset-password",
            new ResetPasswordRequest(userId, newPassword));

    public Task<IReadOnlyList<ReportSummaryDto>> GetAdminReportsAsync() =>
        GetListAsync<ReportSummaryDto>("api/admin/reports");

    public Task<IReadOnlyList<AdminPipelineDto>> GetAdminPipelinesAsync() =>
        GetListAsync<AdminPipelineDto>("api/admin/pipelines");

    public Task<IReadOnlyList<LookupTableDto>> GetAdminLookupsAsync() =>
        GetListAsync<LookupTableDto>("api/admin/lookups");

    public Task<IReadOnlyList<AdminDataSourceDto>> GetAdminDataSourcesAsync() =>
        GetListAsync<AdminDataSourceDto>("api/admin/datasources");

    public Task<IReadOnlyList<AdminQueryDto>> GetAdminQueriesAsync() =>
        GetListAsync<AdminQueryDto>("api/admin/queries");

    private async Task<IReadOnlyList<T>> GetListAsync<T>(string path) =>
        await GetAsync<List<T>>(path) ?? [];

    private async Task<T> GetAsync<T>(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        using var response = await SendAuthenticatedAsync(request);
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException("La API local devolvió una respuesta vacía.");
    }

    private async Task<T> PostAsync<T>(string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        using var response = await SendAuthenticatedAsync(request);
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException("La API local devolvió una respuesta vacía.");
    }

    private async Task PostWithoutResultAsync(string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        using var response = await SendAuthenticatedAsync(request);
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        HttpRequestMessage request, HttpClient? client = null)
    {
        var token = await _tokens.GetTokenAsync();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await (client ?? _http).SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            Logout();
            throw new InvalidOperationException("Sesión GRM vencida o no válida. Inicie sesión nuevamente.");
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            response.Dispose();
            throw new InvalidOperationException("No tiene permiso para esta operación.");
        }
        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            string? detail = null;
            if (client is null)
            {
                try
                {
                    using var body = await JsonDocument.ParseAsync(
                        await response.Content.ReadAsStreamAsync());
                    if (body.RootElement.TryGetProperty("message", out var message)
                        && message.ValueKind == JsonValueKind.String)
                        detail = message.GetString();
                }
                catch (JsonException) { }
            }
            response.Dispose();
            throw new HttpRequestException(string.IsNullOrWhiteSpace(detail)
                ? $"La API respondió HTTP {status}."
                : $"La API respondió HTTP {status}: {detail}");
        }
        return response;
    }

    public void Dispose()
    {
        Logout();
        _tokens.Dispose();
        _http.Dispose();
        _grmCalidadHttp.Dispose();
    }
}

public sealed record GrmRecepcionResponse(int TipoProceso, IReadOnlyList<GrmRecepcionRecord>? Registros);
public sealed record GrmRecepcionRecord(
    string Lote, int? ReportId, DateTime? FechaHoraCreacionArchivo,
    string? Productor, decimal? Kg, string? Estado,
    string? CodigoPallet = null, string? Calibre = null);
public sealed record GrmRecepcionRegistrationItem(
    string Lote, int ReportId, DateTime FechaHoraCreacionArchivo,
    string? Productor, decimal? Kg);
public sealed record GrmCalidadRegistrationItem(
    string Lote, int ReportId, DateTime FechaHoraCreacionArchivo, string? Productor);
public sealed record GrmProduccionRegistrationItem(
    string Lote, int ReportId, string CodigoPallet,
    DateTime FechaHoraCreacionArchivo, string? Productor, string? Calibre);
public sealed record GrmRecepcionRegistrationResponse(
    IReadOnlyList<string> Insertados, IReadOnlyList<string> Existentes,
    IReadOnlyList<GrmPersistenciaIdentity>? InsertadosRegistros = null,
    IReadOnlyList<GrmPersistenciaIdentity>? ExistentesRegistros = null);
public sealed record GrmPersistenciaIdentity(int ReportId, string Lote, string CodigoPallet);
