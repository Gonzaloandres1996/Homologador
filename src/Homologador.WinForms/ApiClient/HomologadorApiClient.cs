using System.Net.Http.Headers;
using System.Net.Http.Json;
using Homologador.Core.DTOs;

namespace Homologador.WinForms.ApiClient;

public class HomologadorApiClient
{
    private readonly HttpClient _http;
    public UserSessionDto? CurrentUser { get; private set; }
    public string? AccessToken { get; private set; }

    public HomologadorApiClient(string baseUrl)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.EndsWith("/") ? baseUrl : baseUrl + "/"),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var resp = await _http.GetAsync("api/health", cts.Token);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("Credenciales inválidas o cuenta inactiva.");
            }
            throw new InvalidOperationException($"Error HTTP {(int)resp.StatusCode} ({resp.StatusCode}) al contactar api/auth/login.");
        }

        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>()
                   ?? throw new InvalidOperationException("Respuesta de autenticación vacía.");

        AccessToken = auth.Token;
        CurrentUser = auth.User;
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        return auth;
    }

    public async Task<IReadOnlyList<ReportSummaryDto>> GetMyReportsAsync()
    {
        return await _http.GetFromJsonAsync<List<ReportSummaryDto>>("api/reports/my-reports") ?? [];
    }

    public async Task<ExecutionContextDto> GetExecutionContextAsync(int reportId)
    {
        return await _http.GetFromJsonAsync<ExecutionContextDto>($"api/reports/{reportId}/execution-context")
               ?? throw new InvalidOperationException("No se pudo obtener el contexto de ejecución del reporte.");
    }

    public async Task<MarkGeneratedResponse> MarkGeneratedAsync(MarkGeneratedRequest request)
    {
        var resp = await _http.PostAsJsonAsync("api/records/mark-generated", request);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<MarkGeneratedResponse>()
               ?? throw new InvalidOperationException("Respuesta inválida al registrar lotes generados.");
    }

    public async Task<IReadOnlyList<RecordGridRowDto>> GetRecordsByAreaAsync(string area, string? search = null, int? reportId = null)
    {
        var url = $"api/records/{Uri.EscapeDataString(area)}";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        }
        if (reportId.HasValue)
        {
            queryParams.Add($"reportId={reportId.Value}");
        }
        if (queryParams.Count > 0)
        {
            url += "?" + string.Join("&", queryParams);
        }
        return await _http.GetFromJsonAsync<List<RecordGridRowDto>>(url) ?? [];
    }

    public async Task<IReadOnlyList<RecordGridRowDto>> GetRecordsByReportAsync(int reportId, string? search = null)
    {
        var url = $"api/records/by-report/{reportId}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"?search={Uri.EscapeDataString(search)}";
        }
        return await _http.GetFromJsonAsync<List<RecordGridRowDto>>(url) ?? [];
    }

    public async Task<IReadOnlyList<AdminUserDto>> GetAdminUsersAsync()
    {
        return await _http.GetFromJsonAsync<List<AdminUserDto>>("api/admin/users") ?? [];
    }

    public async Task AssignUserReportsAsync(int userId, IReadOnlyList<int> reportIds)
    {
        var resp = await _http.PostAsJsonAsync("api/admin/users/assign-reports", new AssignUserReportsRequest(userId, reportIds));
        resp.EnsureSuccessStatusCode();
    }

    public async Task ResetPasswordAsync(int userId, string newPassword)
    {
        var resp = await _http.PostAsJsonAsync("api/admin/users/reset-password", new ResetPasswordRequest(userId, newPassword));
        resp.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ReportSummaryDto>> GetAdminReportsAsync()
    {
        return await _http.GetFromJsonAsync<List<ReportSummaryDto>>("api/admin/reports") ?? [];
    }

    public async Task<IReadOnlyList<AdminPipelineDto>> GetAdminPipelinesAsync()
    {
        return await _http.GetFromJsonAsync<List<AdminPipelineDto>>("api/admin/pipelines") ?? [];
    }

    public async Task<IReadOnlyList<LookupTableDto>> GetAdminLookupsAsync()
    {
        return await _http.GetFromJsonAsync<List<LookupTableDto>>("api/admin/lookups") ?? [];
    }

    public async Task<IReadOnlyList<AdminDataSourceDto>> GetAdminDataSourcesAsync()
    {
        return await _http.GetFromJsonAsync<List<AdminDataSourceDto>>("api/admin/datasources") ?? [];
    }

    public async Task<IReadOnlyList<AdminQueryDto>> GetAdminQueriesAsync()
    {
        return await _http.GetFromJsonAsync<List<AdminQueryDto>>("api/admin/queries") ?? [];
    }
}
