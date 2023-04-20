using Engine;
using Engine.Configuration;
using Engine.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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
        var m = new FileMigrationStartManager(_config, _logger);

        var copyCfg = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/", 
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction);
        var r = await m.StartCopy(copyCfg, new FakeChunkManager());
        Assert.IsNotNull(r);

        await m.MakeCopy(new FileCopyBatch { Files = r, Request = copyCfg }, new FakeFileListProcessor());
    }

    [TestMethod]
    public async Task FileMigrationManagerInvalidArgTests()
    {
        var m = new FileMigrationStartManager(_config, _logger);

        // No folder
        var invalidCfg = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "", 
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction);

        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => await m.StartCopy(invalidCfg, new FakeChunkManager()));
    }
}