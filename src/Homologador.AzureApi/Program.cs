using Homologador.AzureApi;

var app = AzureApiHost.Build(args, AzureApiHost.DefaultBaseUrl);
await app.RunAsync();
