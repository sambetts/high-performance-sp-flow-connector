using Engine;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PnP.Core.Auth;
using PnP.Core.Services;

Console.WriteLine("Test console!");
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", true);
var configCollection = builder.Build();
var config = new Config(configCollection);

var host = new HostBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton(config);

        // Add and configure PnP Core SDK
        services.AddPnPCore(async options =>
        {
            options.HttpRequests.Timeout = 10 * 60;

            // Load the certificate to use
            var cert = await AuthUtils.RetrieveKeyVaultCertificate("AzureAutomationSPOAccess", config.AzureAdConfig.TenantId, config.AzureAdConfig.ClientId, config.AzureAdConfig.ClientSecret, config.KeyVaultUrl);

            options.DefaultAuthenticationProvider = new X509CertificateAuthenticationProvider(config.AzureAdConfig.ClientId, config.AzureAdConfig.TenantId, cert)
            {
                ConfigurationName = "Default"
            };
        });
    })
    .Build();

var _logger = LoggerFactory.Create(config => { config.AddConsole(); }).CreateLogger("Test console");


var testCopyRequest = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents",
               "https://m365x72460609.sharepoint.com/sites/Files", "/Docs/FlowCopy", ConflictResolution.NewDesintationName, false);

var sourceContext = await AuthUtils.GetClientContext(config, testCopyRequest.CurrentWebUrl, _logger, null);
var sourceListGuid = await SPOListLoader.GetListId(new CopyInfo(testCopyRequest.CurrentWebUrl, testCopyRequest.RelativeUrlToCopy), sourceContext, _logger);
var sourceTokenManager = new SPOTokenManager(config, testCopyRequest.CurrentWebUrl, _logger);
var listLoader = new SPOListLoader(sourceListGuid, sourceTokenManager, _logger);

var pnpContextFactory = host.Services.GetRequiredService<IPnPContextFactory>();
using (var context = await pnpContextFactory.CreateAsync(new Uri(testCopyRequest.CurrentWebUrl)))
{
    // Test context
    await context.Web.LoadAsync(p => p.Title);
    Console.WriteLine($"Web title = {context.Web.Title}");

    var m = new FileMigrationManager(_logger);
    await m.StartCopy(testCopyRequest, listLoader, new SharePointAndServiceBusFileResultManager(config, _logger, context));
}
