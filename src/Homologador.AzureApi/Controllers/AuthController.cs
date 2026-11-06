using Homologador.AzureApi.Security;
using Homologador.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public sealed class AuthController(GrmProfileAccess access) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserSessionDto>> Me(CancellationToken ct)
    {
        var result = await access.GetAsync(HttpContext, ct);
        if (result.User is null)
            return StatusCode(result.StatusCode);

        var user = result.User;
        var profiles = user.Roles.Where(role =>
            role is "Conversor_Recepcion" or "Conversor_Calidad" or "Conversor_Produccion");
        return Ok(new UserSessionDto(
            user.Id,
            user.Username,
            user.Email,
            string.Join(" ", new[] { user.Name, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))),
            "Operator",
            string.Join(", ", profiles)));
    }
}
