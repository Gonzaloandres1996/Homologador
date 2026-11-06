using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using Homologador.AzureApi;

namespace Homologador.WinForms.ApiClient;

public sealed class GrmTokenManager : IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _renewal = new(1, 1);
    private string? _username;
    private string? _password;
    private string? _token;
    private DateTimeOffset _expiresAt;
    private int _sessionVersion;

    public GrmTokenManager() : this(new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })) { }

    public GrmTokenManager(HttpClient http)
    {
        _http = http;
        _http.BaseAddress = new Uri(AzureApiHost.GrmAuthenticationBaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task LoginAsync(string username, string password, CancellationToken ct = default)
    {
        Logout();
        var version = _sessionVersion;
        var issued = await AuthenticateAsync(username, password, ct);
        if (version != _sessionVersion)
            throw new InvalidOperationException("La sesión fue cerrada.");
        _username = username;
        _password = password;
        _token = issued.Token;
        _expiresAt = issued.ExpiresAt;
    }

    public async Task<string> GetTokenAsync(CancellationToken ct = default)
    {
        if (_token is null || _username is null || _password is null)
            throw new InvalidOperationException("Debe iniciar sesión.");
        if (_expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return _token;

        await _renewal.WaitAsync(ct);
        try
        {
            if (_token is null || _username is null || _password is null)
                throw new InvalidOperationException("Debe iniciar sesión.");
            if (_expiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
                return _token;

            var version = _sessionVersion;
            try
            {
                var issued = await AuthenticateAsync(_username, _password, ct);
                if (version != _sessionVersion)
                    throw new InvalidOperationException("La sesión fue cerrada.");
                _token = issued.Token;
                _expiresAt = issued.ExpiresAt;
                return _token;
            }
            catch (HttpRequestException) when (_expiresAt > DateTimeOffset.UtcNow)
            {
                // Keep the still valid token and try renewal again on the next request.
                return _token;
            }
            catch (InvalidOperationException)
            {
                if (version == _sessionVersion) Logout();
                throw;
            }
        }
        finally
        {
            _renewal.Release();
        }
    }

    public void Logout()
    {
        Interlocked.Increment(ref _sessionVersion);
        _username = null;
        _password = null;
        _token = null;
        _expiresAt = default;
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)> AuthenticateAsync(
        string username, string password, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync(
            "auth/login", new { user = username, password }, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Credenciales GRM inválidas o cuenta inactiva.");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("GRM denegó el inicio de sesión.");
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"GRM.Authentication respondió HTTP {(int)response.StatusCode}.");

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Respuesta de autenticación GRM vacía.");
        if (string.IsNullOrWhiteSpace(body.AccessToken)
            || !string.Equals(body.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase)
            || body.ExpiresIn <= 0)
            throw new InvalidOperationException("Respuesta de autenticación GRM inválida.");

        DateTimeOffset jwtExpiry;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.AccessToken);
            jwtExpiry = new DateTimeOffset(jwt.ValidTo);
        }
        catch (Exception ex) when (ex is ArgumentException or Microsoft.IdentityModel.Tokens.SecurityTokenException)
        {
            throw new InvalidOperationException("JWT GRM inválido.");
        }
        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(body.ExpiresIn);
        if (jwtExpiry < expiresAt) expiresAt = jwtExpiry;
        if (expiresAt <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("JWT GRM vencido.");
        return (body.AccessToken, expiresAt);
    }

    public void Dispose()
    {
        Logout();
        _http.Dispose();
        _renewal.Dispose();
    }

    private sealed record TokenResponse(string AccessToken, int ExpiresIn, string TokenType);
}
