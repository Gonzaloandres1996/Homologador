using System.Windows.Forms;
using Homologador.AzureApi;
using Homologador.WinForms.ApiClient;
using Homologador.WinForms.Forms;
using Microsoft.AspNetCore.Builder;

namespace Homologador.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var apiUrl = AzureApiHost.DefaultBaseUrl;
        var apiClient = new HomologadorApiClient(apiUrl);

        WebApplication? embeddedApi = null;
        if (!apiClient.IsHealthyAsync().GetAwaiter().GetResult())
        {
            embeddedApi = AzureApiHost.Build(args, apiUrl);
            AzureApiHost.EnsureDefaultUsersReadyAsync(embeddedApi.Services).GetAwaiter().GetResult();
            embeddedApi.StartAsync().GetAwaiter().GetResult();
        }

        try
        {
            while (true)
            {
                using var loginForm = new LoginForm(apiClient);
                if (loginForm.ShowDialog() != DialogResult.OK)
                {
                    break;
                }

                using var mainForm = new MainForm(apiClient);
                Application.Run(mainForm);

                if (!mainForm.SwitchUserRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            if (embeddedApi != null)
            {
                embeddedApi.StopAsync().GetAwaiter().GetResult();
                embeddedApi.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }
    }
}
