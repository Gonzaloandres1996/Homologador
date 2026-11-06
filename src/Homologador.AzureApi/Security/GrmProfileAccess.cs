using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Homologador.AzureApi.Security;

public sealed record GrmCompany(int Id);

public sealed record GrmUser(
    int Id,
    string Email,
    string Username,
    string Name,
    string LastName,
    GrmCompany Company,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record GrmAccessResult(int StatusCode, GrmUser? User)
{
    public bool HasArea(string? area)
    {
        if (StatusCode != StatusCodes.Status200OK || User is null) return false;
        var (role, permission) = area switch
        {
            "Recepción" => ("Conversor_Recepcion", "GRM.CONVERSOR.RECEPCION"),
            "Calidad" => ("Conversor_Calidad", "GRM.CONVERSOR.CALIDAD"),
            "Producción" => ("Conversor_Produccion", "GRM.CONVERSOR.PRODUCCION"),
            _ => (string.Empty, string.Empty)
        };
        return role.Length > 0
            && User.Roles.Contains(role, StringComparer.Ordinal)
            && User.Permissions.Contains(permission, StringComparer.Ordinal);
    }
}

public sealed class GrmProfileAccess(HttpClient client)
{
    private static readonly object CacheKey = new();

    public async Task<GrmAccessResult> GetAsync(HttpContext context, CancellationToken ct)
    {
        if (context.Items.TryGetValue(CacheKey, out var cached) && cached is GrmAccessResult result)
            return result;

        result = await FetchAsync(context, ct);
        context.Items[CacheKey] = result;
        return result;
    }

    private async Task<GrmAccessResult> FetchAsync(HttpContext context, CancellationToken ct)
    {
        if (!AuthenticationHeaderValue.TryParse(
                context.Request.Headers.Authorization.ToString(), out var authorization)
            || !string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
            return new(StatusCodes.Status401Unauthorized, null);

        using var request = new HttpRequestMessage(HttpMethod.Get, "user");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authorization.Parameter);
        try
        {
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return new(StatusCodes.Status401Unauthorized, null);
            if (response.StatusCode == HttpStatusCode.Forbidden)
                return new(StatusCodes.Status403Forbidden, null);
            if (!response.IsSuccessStatusCode)
                return new(StatusCodes.Status503ServiceUnavailable, null);

            var user = await response.Content.ReadFromJsonAsync<GrmUser>(cancellationToken: ct);
            if (user is null || user.Id <= 0 || user.Company?.Id <= 0
                || string.IsNullOrWhiteSpace(user.Email)
                || string.IsNullOrWhiteSpace(user.Username)
                || user.Roles is null || user.Permissions is null)
                return new(StatusCodes.Status401Unauthorized, null);

            return new(StatusCodes.Status200OK, user);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return new(StatusCodes.Status503ServiceUnavailable, null);
        }
        catch (OperationCanceledException)
        {
            return new(StatusCodes.Status503ServiceUnavailable, null);
        }
        catch (System.Text.Json.JsonException)
        {
            return new(StatusCodes.Status503ServiceUnavailable, null);
        }
    }
}
