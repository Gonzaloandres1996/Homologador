using Homologador.AzureApi.Data;
using Homologador.AzureApi.Security;
using Homologador.AzureApi.Services;
using Homologador.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Homologador.AzureApi;

public static class AzureApiHost
{
    public const string DefaultBaseUrl = "http://localhost:5080";
    public const string GrmAuthenticationBaseUrl = "https://apsv-prd-grm-authentication-ddd4faddfwdjdgbz.eastus2-01.azurewebsites.net/";
    public const string GrmCalidadBaseUrl = "https://apsv-prd-grm-calidad-hde4brayaphcdca9.eastus2-01.azurewebsites.net/";
    public const string DefaultAzureDb = "Server=localhost;Database=HomologatorAzure;Trusted_Connection=True;TrustServerCertificate=True;";
    public const string DefaultEncryptionKey = "/cv1xATwDdZyG+Hr8AE43a9dEHrw5KMC2Zsgobdyq0K0YoFUo/W3+J83MhoELZPr";

    public static WebApplication Build(string[] args, string? urls = null)
    {
        var builder = WebApplication.CreateBuilder(args);

        if (!string.IsNullOrWhiteSpace(urls))
        {
            builder.WebHost.UseUrls(urls);
        }

        var azureConnStr = builder.Configuration.GetConnectionString("AzureDb");
        if (string.IsNullOrWhiteSpace(azureConnStr) || azureConnStr.Contains("Database=DataHomologator;", StringComparison.OrdinalIgnoreCase))
        {
            azureConnStr = DefaultAzureDb;
        }

        var encKey = builder.Configuration["Encryption:Key"] ?? DefaultEncryptionKey;
        builder.Services.AddDbContext<AzureDbContext>(opt => opt.UseSqlServer(azureConnStr));
        builder.Services.AddSingleton<IEncryptionService>(_ => new EncryptionService(encKey));
        builder.Services.AddSingleton<ISqlConnectionTester, SqlConnectionTester>();
        builder.Services.AddSingleton<IExternalQueryExecutor, ExternalQueryExecutor>();
        builder.Services.AddSingleton<ITransformationEngine, TransformationEngine>();
        builder.Services.AddSingleton<IFileGeneratorService, FileGeneratorService>();
        builder.Services.AddHttpClient<GrmProfileAccess>(client =>
        {
            client.BaseAddress = new Uri(GrmAuthenticationBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });
        builder.Services.AddHttpClient<GrmRuleReader>(client =>
        {
            client.BaseAddress = new Uri(GrmCalidadBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        });

        builder.Services
            .AddAuthentication(GrmAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, GrmAuthenticationHandler>(
                GrmAuthenticationHandler.SchemeName, _ => { });

        builder.Services.AddAuthorization(options =>
            options.AddPolicy("HomologadorAdmin", policy =>
                policy.RequireAssertion(_ => false)));
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(AzureApiHost).Assembly);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "Online",
            authentication = "GRM",
            configDb = "GRM",
            azureDb = "HomologatorAzure"
        }));

        return app;
    }
}
