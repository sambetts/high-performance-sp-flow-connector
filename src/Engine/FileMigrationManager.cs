using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;
using System.Diagnostics;

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

        var sourceInfo = new CopyInfo(startCopyInfo);

        var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentSite, _logger);
        var spClient = await sourceTokenManager.GetOrRefreshContext();

        var list = spClient.Web.GetListUsingPath(ResourcePath.FromDecodedUrl(sourceInfo.ListUrl));

        try
        {
            spClient.Load(list, l => l.Id, l => l.Title);
            await spClient.ExecuteQueryAsyncWithThrottleRetries(_logger);
        }
        catch (System.Net.WebException ex)
        {
            _logger.LogError($"Got exception '{ex.Message}' loading data for list URL '{sourceInfo.ListUrl}'.");
            throw;
        }

        await spClient.ExecuteQueryAsyncWithThrottleRetries(_logger);

        var crawler = new SiteListsAndLibrariesCrawler<ListItemCollectionPosition>(_logger);
        await crawler.CrawlList(new SPOListLoader(list, sourceTokenManager, _logger), null);
    }
}
