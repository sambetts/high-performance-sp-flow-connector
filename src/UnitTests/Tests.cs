using Engine;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
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
        var m = new FileMigrationStartManager(new FakeChunkManager(), _config, _logger);

        var r = await m.StartCopy(new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/", 
            "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction));
        Assert.IsNotNull(r);
    }

    [TestMethod]
    public async Task FileMigrationManagerInvalidArgTests()
    {
        var m = new FileMigrationStartManager(new FakeChunkManager(), _config, _logger);

        // No folder
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(async () => 
            await m.StartCopy(new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "", "https://m365x72460609.sharepoint.com/sites/Files", "", ConflictResolution.FailAction)));
    }

    public class FakeChunkManager : IFileResultManager
    {
        public Task ProcessChunk(FileCopyBatch fileCopyBatch)
        {
            return Task.CompletedTask;
        }
    }
}