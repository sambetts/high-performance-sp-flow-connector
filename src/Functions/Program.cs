using Engine.Configuration;
using Engine.SharePoint;
using Engine.Utils;
using Functions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PnP.Core.Auth;


var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true);
var configCollection = builder.Build();
var config = new Config(configCollection);


// Load the certificate to use
var cert = await AuthUtils.RetrieveKeyVaultCertificate("AzureAutomationSPOAccess", config.AzureAdConfig.TenantId, config.AzureAdConfig.ClientId, config.AzureAdConfig.ClientSecret, config.KeyVaultUrl);

var host = new HostBuilder()
    .ConfigureAppConfiguration(c =>
    {
        c.AddEnvironmentVariables()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
            .AddCommandLine(args)
            .Build();
    })
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton(config);
        services.AddSingleton<SharePointFileMigrationManager<SBFunctions>>();
        services.AddSingleton<TaskQueueManager>();

        // Add and configure PnP Core SDK
        services.AddPnPCore(options =>
        {
            options.HttpRequests.Timeout = 30 * 60;

            options.DefaultAuthenticationProvider = new X509CertificateAuthenticationProvider(config.AzureAdConfig.ClientId, config.AzureAdConfig.TenantId, cert)
            {
                ConfigurationName = "Default"
            };
        });
    })
    .Build();

host.Run();
