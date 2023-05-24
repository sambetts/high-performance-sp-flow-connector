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
    const int MAX_FILES_PER_BATCH = 5;

    public FileMigrationManager(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopy<PAGETOKENTYPE>(StartCopyRequest startCopyInfo, IListLoader<PAGETOKENTYPE> listLoader, IFileResultManager chunkProcessor)
    {
        // Parse command into usable objects
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentWebUrl, startCopyInfo.RelativeUrlToCopy);
        var destInfo = new CopyInfo(startCopyInfo.DestinationWebUrl, startCopyInfo.RelativeUrlDestination);

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


    public async Task<List<string>> CompleteCopy(FileCopyBatch batch, IFileListProcessor fileListProcessor)
    {
        const int MAX_FAIL_COUNT = 3;
        var timer = new JobTimer(_logger, nameof(CompleteCopy));
        timer.Start();
        await fileListProcessor.Init();

        var failedFiles = new Dictionary<SharePointFileInfoWithList, int>();
        var filesToProcess = new List<SharePointFileInfoWithList>(batch.Files);
        var throttleStats = new FilesUploadResults();

        while (filesToProcess.Count > 0)
        {
            foreach (var sourceFileToCopy in filesToProcess)
            {
                var fileSuccess = false;
                try
                {
                    var urlStats = await fileListProcessor.ProcessFile(sourceFileToCopy, batch.Request);
                    throttleStats.Add(urlStats);
                    fileSuccess = true;
                }
                catch (Exception ex)
                {
                    var failCount = 0;
                    if (failedFiles.ContainsKey(sourceFileToCopy))
                    {
                        failCount = failedFiles[sourceFileToCopy];
                    }
                    else
                    {
                        failedFiles.Add(sourceFileToCopy, failCount);
                    }
                    failCount++;
                    failedFiles[sourceFileToCopy] = failCount;
                    if (failCount == MAX_FAIL_COUNT)
                    {
                        _logger.LogError($"Got unexpected error #{failCount} '{ex.Message}' on {sourceFileToCopy.FullSharePointUrl}. Giving up.");
                    }
                    else
                    {
                        _logger.LogError($"Got unexpected error #{failCount} '{ex.Message}' on {sourceFileToCopy.FullSharePointUrl}.");
                    }
                }
                if (fileSuccess)
                {
                    failedFiles.Remove(sourceFileToCopy);
                }
            }

            // Retry any files that failed, below max fail threshold
            filesToProcess = failedFiles.Where(x => x.Value < MAX_FAIL_COUNT).Select(x => x.Key).ToList();
        }

        Print(ThottleUploadStage.ListLookup, throttleStats.Throttling);
        Print(ThottleUploadStage.UploadFile, throttleStats.Throttling);
        Print(ThottleUploadStage.FolderCreate, throttleStats.Throttling);

        timer.StopAndPrintElapsed();
        _logger.LogInformation($"Copied {batch.Files.Count} files to destination.");
        return throttleStats.FilesCreated;
    }

    void Print(ThottleUploadStage stage, Dictionary<ThottleUploadStage, int> datat)
    {
        var r = datat.Where(t => t.Key == stage).ToList();
        if (r.Count > 0)
        {
            _logger.LogInformation($"Throttle stats: {Enum.GetName(stage)}: {r.Count}");
        }
    }
}

public class FileCopyBatch
{
    public StartCopyRequest Request { get; set; } = null!;

    public List<SharePointFileInfoWithList> Files { get; set; } = new();
    public bool IsValid => Files.Count > 0 && Request != null && Request.IsValid;

    internal string ToJson()
    {
        // Convert to json this object
        return System.Text.Json.JsonSerializer.Serialize(this);

    }
}
