using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine;

public class FileMigrationManager
{
    private readonly Config _config;
    private readonly ILogger _logger;

    public FileMigrationManager(Config config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task StartCopy(StartCopyRequest startCopyInfo)
    {
        // Parse command into usable objects
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentSite, startCopyInfo.RelativeUrlToCopy);
        var destInfo = new CopyInfo(startCopyInfo.DestinationSite, startCopyInfo.RelativeUrlToCopy);

        var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentSite, _logger);
        var spClient = await sourceTokenManager.GetOrRefreshContext();

        var lists = await GetSourceAndDestinationLists(sourceInfo, destInfo, spClient);

        // Get source files
        var crawler = new SiteListsAndLibrariesCrawler<ListItemCollectionPosition>(_logger);
        var sourceFiles = await crawler.CrawlList(new SPOListLoader(lists.Item1, sourceTokenManager, _logger), null);
        _logger.LogInformation($"Copying {sourceFiles.FilesFound.Count} files in list '{lists.Item1.Title}'.");


    }

    async Task<(List, List)> GetSourceAndDestinationLists(CopyInfo sourceInfo, CopyInfo destInfo, ClientContext spClient)
    {

        var sourceList = spClient.Web.GetListUsingPath(ResourcePath.FromDecodedUrl(sourceInfo.ListUrl));
        try
        {
            spClient.Load(sourceList, l => l.Id, l => l.Title);
            await spClient.ExecuteQueryAsyncWithThrottleRetries(_logger);
        }
        catch (System.Net.WebException ex)
        {
            _logger.LogError($"Got exception '{ex.Message}' loading data for source list URL '{sourceInfo.ListUrl}'.");
            throw;
        }
        await spClient.ExecuteQueryAsyncWithThrottleRetries(_logger);

        var destList = spClient.Web.GetListUsingPath(ResourcePath.FromDecodedUrl(destInfo.ListUrl));
        try
        {
            spClient.Load(destList, l => l.Id, l => l.Title);
            await spClient.ExecuteQueryAsyncWithThrottleRetries(_logger);
        }
        catch (System.Net.WebException ex)
        {
            _logger.LogError($"Got exception '{ex.Message}' loading data for destination list URL '{destInfo.ListUrl}'.");
            throw;
        }
        await spClient.ExecuteQueryAsyncWithThrottleRetries(_logger);

        return (sourceList, destList);
    }
}
