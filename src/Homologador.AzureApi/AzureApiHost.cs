using System.Text;
using Homologador.AzureApi.Data;
using Homologador.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Homologador.AzureApi;

public static class AzureApiHost
{
    public const string DefaultBaseUrl = "http://localhost:5080";
    public const string DefaultConfigDb = "Server=.\\SQLEXPRESS;Database=DataHomologator_Migracion;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string DefaultEncryptionKey = "/cv1xATwDdZyG+Hr8AE43a9dEHrw5KMC2Zsgobdyq0K0YoFUo/W3+J83MhoELZPr";
    public const string DefaultJwtSecret = "uwfKkYR04O/12xU3TnpX26JhiO+mhj7maa5H7utyGt1SsYJU2N18FCFmeF+tMvEu8ot5JOmu4cMMWc81hYGhxg==";

    public static WebApplication Build(string[] args, string? urls = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (!string.IsNullOrWhiteSpace(urls))
        {
            builder.WebHost.UseUrls(urls);
        }

        var connStr = builder.Configuration.GetConnectionString("MigracionDb")
                      ?? builder.Configuration.GetConnectionString("ConfigDb")
                      ?? DefaultConfigDb;
        if (!connStr.Contains("DataHomologator_Migracion", StringComparison.OrdinalIgnoreCase))
        {
            connStr = DefaultConfigDb;
        }
        var encKey = builder.Configuration["Encryption:Key"] ?? DefaultEncryptionKey;
        var jwtSecret = builder.Configuration["Jwt:Secret"] ?? DefaultJwtSecret;
        var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "Homologador.AzureApi";
        var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "Homologador.WinForms";

        builder.Services.AddDbContext<MigracionDbContext>(opt => opt.UseSqlServer(connStr));
        builder.Services.AddSingleton<IEncryptionService>(_ => new EncryptionService(encKey));
        builder.Services.AddSingleton<ISqlConnectionTester, SqlConnectionTester>();
        builder.Services.AddSingleton<IExternalQueryExecutor, ExternalQueryExecutor>();
        builder.Services.AddSingleton<ITransformationEngine, TransformationEngine>();
        builder.Services.AddSingleton<IFileGeneratorService, FileGeneratorService>();

        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
                };
            });

        builder.Services.AddAuthorization();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(AzureApiHost).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/api/health", () => Results.Ok(new { status = "Online", database = "DataHomologator_Migracion" }));

        return app;
    }

    public static async Task EnsureDefaultUsersReadyAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MigracionDbContext>();

        var users = await db.Users.ToListAsync();
        bool changed = false;
        foreach (var u in users)
        {
            bool valid = false;
            try
            {
                valid = BCrypt.Net.BCrypt.Verify("admin123.", u.PasswordHash);
            }
            catch
            {
                valid = false;
            }

            if (!valid)
            {
                u.PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123.");
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }
}
