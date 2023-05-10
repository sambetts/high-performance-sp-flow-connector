// See https://aka.ms/new-console-template for more information
using Engine;
using Engine.Code;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

Console.WriteLine("Hello, World!");

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddUserSecrets(System.Reflection.Assembly.GetExecutingAssembly())
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", true);
var configCollection = builder.Build();
var _config = new Config(configCollection);

var _logger = LoggerFactory.Create(config =>
{
    config.AddConsole();
}).CreateLogger("Unit tests");
var copyCfg = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents",
               "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.NewDesintationName);

var sourceContext = await AuthUtils.GetClientContext(_config, copyCfg.CurrentWebUrl, _logger, null);
var sourceListGuid = await SPOListLoader.GetListId(new CopyInfo(copyCfg.CurrentWebUrl, copyCfg.RelativeUrlToCopy), sourceContext, _logger);
var sourceCrawler = new DataCrawler<ListItemCollectionPosition>(_logger);
var sourceTokenManager = new SPOTokenManager(_config, copyCfg.CurrentWebUrl, _logger);

var sourceFiles = await sourceCrawler.CrawlListAllPages(new SPOListLoader(sourceListGuid, sourceTokenManager, _logger), copyCfg.RelativeUrlToCopy);

var fileCopier = new SharePointFileListProcessor(_config, _logger);
await fileCopier.CopyToDestination(new FileCopyBatch { Files = sourceFiles.FilesFound, Request = copyCfg });

