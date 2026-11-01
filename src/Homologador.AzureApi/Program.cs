using Homologador.AzureApi;

var app = AzureApiHost.Build(args, AzureApiHost.DefaultBaseUrl);
await AzureApiHost.EnsureDefaultUsersReadyAsync(app.Services);
await app.RunAsync();
