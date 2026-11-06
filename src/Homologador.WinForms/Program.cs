using System.Windows.Forms;
using Homologador.AzureApi;
using Homologador.WinForms.ApiClient;
using Homologador.WinForms.Forms;
using Microsoft.AspNetCore.Builder;
using Velopack;
using Velopack.Sources;

namespace Homologador.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();

        ApplicationConfiguration.Initialize();

        CheckForUpdates();

        var apiUrl = AzureApiHost.DefaultBaseUrl;
        using var apiClient = new HomologadorApiClient(apiUrl);

        WebApplication? embeddedApi = null;
        if (!apiClient.IsHealthyAsync().GetAwaiter().GetResult())
        {
            embeddedApi = AzureApiHost.Build(args, apiUrl);
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
                apiClient.Logout();

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

    private static void CheckForUpdates()
    {
        try
        {
            var updateSource = new GithubSource("https://github.com/Gonzaloandres1996/Homologador", null, false);
            var mgr = new UpdateManager(updateSource);

            if (!mgr.IsInstalled)
            {
                return;
            }

            var newVersion = mgr.CheckForUpdates();
            if (newVersion != null)
            {
                mgr.DownloadUpdates(newVersion);
                mgr.ApplyUpdatesAndRestart(newVersion);
                Environment.Exit(0);
            }
        }
        catch
        {
            // Si no hay conexión o falla el acceso a GitHub,
            // la aplicación continúa normalmente con la versión actual instalada.
        }
    }
}
