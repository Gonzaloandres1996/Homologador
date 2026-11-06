using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Homologador.AzureApi.Security;

public sealed class GrmAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    GrmProfileAccess access)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "GrmDelegated";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var result = await access.GetAsync(Context, Context.RequestAborted);
        if (result.User is null)
            return AuthenticateResult.Fail("GRM rejected or could not validate the token.");

        var user = result.User;
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("company_id", user.Company.Id.ToString(CultureInfo.InvariantCulture))
        };
        var identity = new ClaimsIdentity(claims, SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var result = await access.GetAsync(Context, Context.RequestAborted);
        Response.StatusCode = result.StatusCode == StatusCodes.Status200OK
            ? StatusCodes.Status401Unauthorized
            : result.StatusCode;
    }
}
