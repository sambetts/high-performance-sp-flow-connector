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
    const int MAX_FILES_PER_BATCH = 30000;      // https://pnp.github.io/pnpcore/using-the-sdk/sites-copymovecontent.html#limitations

    public FileMigrationManager(ILogger logger)
    {
        _logger = logger;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopy<PAGETOKENTYPE>(StartCopyRequest startCopyInfo, IListLoader<PAGETOKENTYPE> listLoader, IFileResultManager filesProcessor)
    {
        // Parse command into usable objects
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentWebUrl, startCopyInfo.RelativeUrlToCopy);
        var destInfo = new CopyInfo(startCopyInfo.DestinationWebUrl, startCopyInfo.RelativeUrlDestination);

        // Get source files
        var crawler = new DataCrawler<PAGETOKENTYPE>(_logger);
        var sourceFiles = await crawler.CrawlListAllPages(listLoader, startCopyInfo.RelativeUrlToCopy);
        _logger.LogInformation($"Copying {sourceFiles.FilesFound.Count} files.");

        // Push large files to queue in batches
        var largeFilesListProcessor = new ListBatchProcessor<SharePointFileInfoWithList>(MAX_FILES_PER_BATCH, async (List<SharePointFileInfoWithList> chunk) => 
        {
            // Create a new class to process each chunk and send to service bus
            await filesProcessor.ProcessLargeFiles(new FileCopyBatch { Files = chunk, Request = startCopyInfo });
        });

        // Process large files on service-bus using CSOM
        largeFilesListProcessor.AddRange(sourceFiles.FilesFound.GetLargeFiles());
        largeFilesListProcessor.Flush();

        // Push large files to queue in batches
        var rootFilesListProcessor = new ListBatchProcessor<string>(MAX_FILES_PER_BATCH, 
            async(List<string> files) => await filesProcessor.ProcessRootFiles(new BaseItemsCopyBatch { FilesAndDirs = files, Request = startCopyInfo }) );

        // Process root files & folders directly with SP copy API
        var rootFiles = sourceFiles.GetRootFilesAndFoldersBelowTwoGig();
        rootFilesListProcessor.AddRange(rootFiles);
        rootFilesListProcessor.Flush();

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

public abstract class BaseCopyBatch
{
    public StartCopyRequest Request { get; set; } = null!;

    public virtual bool IsValid => Request != null && Request.IsValid;
    internal string ToJson()
    {
        // Convert to json this object
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
}

public class FileCopyBatch : BaseCopyBatch
{
    public List<SharePointFileInfoWithList> Files { get; set; } = new();
    public override bool IsValid => Files.Count > 0 && base.IsValid;

}

public class BaseItemsCopyBatch : BaseCopyBatch
{
    public List<string> FilesAndDirs { get; set; } = new();
    public override bool IsValid => FilesAndDirs.Count > 0 && base.IsValid;

}
