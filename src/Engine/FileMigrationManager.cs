using Engine.Code;
using Engine.Core;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Extensions.Logging;

namespace Engine;

public class FileMigrationManager
{
    protected readonly ILogger _logger;
    const int MAX_FILES_PER_BATCH = 100;

    public FileMigrationManager(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopy<PAGETOKENTYPE>(StartCopyRequest startCopyInfo, IListLoader<PAGETOKENTYPE> listLoader, IFileResultManager chunkProcessor)
    {
        // Parse command into usable objects
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentSite, startCopyInfo.RelativeUrlToCopy);
        var destInfo = new CopyInfo(startCopyInfo.DestinationSite, startCopyInfo.RelativeUrlDestination);

        // Get source files
        var crawler = new DataCrawler<PAGETOKENTYPE>(_logger);
        var sourceFiles = await crawler.CrawlListAllPages(listLoader, startCopyInfo.RelativeUrlToCopy);
        _logger.LogInformation($"Copying {sourceFiles.FilesFound.Count} files.");

        // Push to queue in batches
        var l = new ListBatchProcessor<SharePointFileInfoWithList>(MAX_FILES_PER_BATCH, async (List<SharePointFileInfoWithList> chunk) => 
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
        await fileListProcessor.CopyToDestination(batch);
        _logger.LogInformation($"Copied {batch.Files.Count} files.");
    }
}

public class FileCopyBatch
{
    public StartCopyRequest Request { get; set; } = null!;

    public List<SharePointFileInfoWithList> Files { get; set; } = new();

    internal string ToJson()
    {
        // Convert to json this object
        return System.Text.Json.JsonSerializer.Serialize(this);

    }
}
