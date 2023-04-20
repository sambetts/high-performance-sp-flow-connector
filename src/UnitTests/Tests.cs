using Engine;
using Engine.Configuration;
using Engine.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace UnitTests;

[TestClass]
public class Tests
{
    private ILogger _logger;
    private Config _config;
    public Tests()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(System.IO.Directory.GetCurrentDirectory())
            .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", true);
        var configCollection = builder.Build();
        _config = new Config(configCollection);


        _logger = LoggerFactory.Create(config =>
        {
            config.AddConsole();
        }).CreateLogger("Unit tests");
    }

    [TestMethod]
    public async Task FileMigrationManagerTests()
    {
        var m = new FileMigrationManager(_config, _logger);

        await m.StartCopy(new Engine.Models.StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/", 
            "https://m365x72460609.sharepoint.com/sites/Files", Engine.Models.ConflictResolution.FailAction));
    }

    [TestMethod]
    public async Task FileMigrationManagerInvalidArgTests()
    {
        var m = new FileMigrationManager(_config, _logger);

        // No folder
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await m.StartCopy(new Engine.Models.StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "", "https://m365x72460609.sharepoint.com/sites/Files", Engine.Models.ConflictResolution.FailAction)));
    }
}