using Engine.Code;
using Engine.Configuration;
using Engine.Core;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine;

public class FileMigrationStartManager
{
    private readonly Config _config;
    private readonly ILogger _logger;

    public FileMigrationStartManager(Config config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopy<PAGETOKENTYPE>(StartCopyRequest startCopyInfo, IListLoader<PAGETOKENTYPE> listLoader, IFileResultManager chunkProcessor)
    {
        // Parse command into usable objects
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentSite, startCopyInfo.RelativeUrlToCopy);
        var destInfo = new CopyInfo(startCopyInfo.DestinationSite, startCopyInfo.RelativeUrlDestination);

        var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentSite, _logger);
        var spClient = await sourceTokenManager.GetOrRefreshContext();

        // Get source files
        var crawler = new DataCrawler<PAGETOKENTYPE>(_logger);
        var sourceFiles = await crawler.CrawlList(listLoader);
        //_logger.LogInformation($"Copying {sourceFiles.FilesFound.Count} files in list '{lists.Item1.Title}'.");

        // Push to queue in batches
        var l = new ListBatchProcessor<SharePointFileInfoWithList>(1000, async (List<SharePointFileInfoWithList> chunk) => 
        {
            // Create a new class to process each chunk and send to service bus
            await chunkProcessor.ProcessChunk(new FileCopyBatch { Files = chunk, Request = startCopyInfo });
        });

        // Process all files
        l.AddRange(sourceFiles.FilesFound);
        l.Flush();

        return sourceFiles.FilesFound;
    }

    public async Task MakeCopy(FileCopyBatch batch, IFileListProcessor fileListProcessor)
    {
        await fileListProcessor.Copy(batch);
        _logger.LogInformation($"Copied {batch.Files.Count} files.");
    }
}

public class FileCopyBatch
{
    public StartCopyRequest Request { get; set; } = null!;

    public List<SharePointFileInfoWithList> Files { get; set; } = new();
}

public class SharePointFileListProcessor : IFileListProcessor
{
    public Task Copy(FileCopyBatch batch)
    {
        throw new NotImplementedException();
    }
}