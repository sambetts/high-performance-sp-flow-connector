using Engine.Configuration;
using Engine.SharePoint;
using Engine.Utils;
using Functions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PnP.Core.Auth;
using PnP.Core.Auth.Services.Builder.Configuration;
using System.Runtime.ConstrainedExecution;

var host = new HostBuilder()
    .ConfigureAppConfiguration(c =>
    {
        c.AddEnvironmentVariables()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddCommandLine(args)
            .Build();
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var config = new Config(context.Configuration);
        services.AddSingleton(config);
        services.AddSingleton<SharePointFileMigrationManager<SBFunctions>>();


        // Add and configure PnP Core SDK
        services.AddPnPCore(async options =>
        {
            // Load the certificate to use
            var cert = await AuthUtils.RetrieveKeyVaultCertificate("AzureAutomationSPOAccess", config.AzureAdConfig.TenantId, config.AzureAdConfig.ClientId, config.AzureAdConfig.ClientSecret, config.KeyVaultUrl);

            options.DefaultAuthenticationProvider = new X509CertificateAuthenticationProvider(config.AzureAdConfig.ClientId, config.AzureAdConfig.TenantId, cert)
            {
                ConfigurationName = "Default"
            };
        });
    })
    .Build();

host.Run();
