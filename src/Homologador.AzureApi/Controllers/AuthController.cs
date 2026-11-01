using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Homologador.AzureApi.Data;
using Homologador.Core.DTOs;
using Homologador.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Homologador.AzureApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly MigracionDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(MigracionDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Correo y contraseña son obligatorios." });
        }

        var input = request.Email.Trim();
        var prefixWithAt = input.Contains('@') ? null : input + "@";
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Email == input ||
            u.DisplayName == input ||
            (prefixWithAt != null && u.Email.StartsWith(prefixWithAt)), ct);
        if (user == null || !user.IsActive || !VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Credenciales inválidas o cuenta inactiva." });
        }

        return Ok(BuildAuthResponse(user));
    }

    private static bool VerifyPassword(string? password, string hash)
    {
        if (string.IsNullOrEmpty(password)) return false;
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            return false;
        }
    }

    private AuthResponse BuildAuthResponse(User user)
    {
        var secret = _config["Jwt:Secret"] ?? AzureApiHost.DefaultJwtSecret;
        var issuer = _config["Jwt:Issuer"] ?? "Homologador.AzureApi";
        var audience = _config["Jwt:Audience"] ?? "Homologador.WinForms";
        var expMinutes = int.TryParse(_config["Jwt:ExpirationMinutes"], out var m) ? m : 480;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, user.Rol),
            new("codigo", user.Codigo),
            new("perfil", user.Perfil ?? string.Empty)
        };

        var expiresAt = DateTime.UtcNow.AddMinutes(expMinutes);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(tokenDescriptor);

        return new AuthResponse(
            Token: handler.WriteToken(token),
            ExpiresAt: expiresAt,
            User: new UserSessionDto(
                user.Id,
                user.Codigo,
                user.Email,
                user.DisplayName,
                user.Rol,
                user.Perfil
            )
        );
    }
}
