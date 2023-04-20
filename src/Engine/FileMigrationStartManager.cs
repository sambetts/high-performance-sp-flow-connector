using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine;

public class FileMigrationStartManager
{
    private readonly IFileResultManager _chunkProcessor;
    private readonly Config _config;
    private readonly ILogger _logger;

    public FileMigrationStartManager(IFileResultManager chunkProcessor, Config config, ILogger logger)
    {
        _chunkProcessor = chunkProcessor;
        _config = config;
        _logger = logger;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopy(StartCopyRequest startCopyInfo)
    {
        // Parse command into usable objects
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentSite, startCopyInfo.RelativeUrlToCopy);
        var destInfo = new CopyInfo(startCopyInfo.DestinationSite, startCopyInfo.RelativeUrlDestination);

        var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentSite, _logger);
        var spClient = await sourceTokenManager.GetOrRefreshContext();

        var lists = await GetSourceAndDestinationLists(sourceInfo, destInfo, spClient);

        // Get source files
        var crawler = new SiteListsAndLibrariesCrawler<ListItemCollectionPosition>(_logger);
        var sourceFiles = await crawler.CrawlList(new SPOListLoader(lists.Item1, sourceTokenManager, _logger), null);
        _logger.LogInformation($"Copying {sourceFiles.FilesFound.Count} files in list '{lists.Item1.Title}'.");

        // Push to queue in batches
        var l = new ListBatchProcessor<SharePointFileInfoWithList>(1000, async (List<SharePointFileInfoWithList> chunk) => 
        {
            // Create a new class to process each chunk and send to service bus
            await _chunkProcessor.ProcessChunk(new FileCopyBatch { Files = chunk, Request = startCopyInfo });
        });

        // Process all files
        l.AddRange(sourceFiles.FilesFound);
        l.Flush();

        return sourceFiles.FilesFound;
    }

    public async Task MakeCopy(FileCopyBatch batch)
    {

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

public class FileCopyBatch
{
    public StartCopyRequest Request { get; set; } = null!;

    public List<SharePointFileInfoWithList> Files { get; set; } = new();
}
