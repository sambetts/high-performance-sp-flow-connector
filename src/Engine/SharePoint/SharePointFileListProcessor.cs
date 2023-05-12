using Engine.Configuration;
using Engine.Core;
using Engine.Models;
using Engine.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine.SharePoint;

public class SharePointFileListProcessor : IFileListProcessor
{
    private readonly ILogger _logger;
    private readonly ClientContext _clientDest;
    private readonly Config _config;

    public SharePointFileListProcessor(Config config, ILogger logger, ClientContext clientDest)
    {
        _logger = logger;
        _clientDest = clientDest;
        _config = config;
    }
    public async Task CopyToDestination(FileCopyBatch batch)
    {

        var app = await AuthUtils.GetNewClientApp(_config);
        var downloader = new SharePointFileDownloader(app, _config, _logger);

        await CopyFiles(downloader, batch.Files, batch.Request);
        
    }

    private async Task CopyFiles(SharePointFileDownloader downloader, List<SharePointFileInfoWithList> files, StartCopyRequest request)
    {
        var listCache = new ListCache(_clientDest, _logger);
        var folderCache = new FolderCache(_clientDest, _logger);
        foreach (var sourceFileToCopy in files)
        {
            using (var sourceFileStream = await downloader.DownloadAsStream(sourceFileToCopy))
            {
                var destFileInfo = sourceFileToCopy.ConvertFromForSameSiteCollection(request);
                var destFilePathInfo = ServerRelativeFilePathInfo.FromServerRelativeFilePath(destFileInfo.ServerRelativeFilePath);

                var destList = await listCache.GetByServerRelativeUrl(destFileInfo.List.ServerRelativeUrl);

                // Figure out target folder name & create it if needed
                var rootDestFolderName = destFilePathInfo.FolderPath.TrimStringFromStart(destList.RootFolder.ServerRelativeUrl);
                if (!rootDestFolderName.EndsWith("/"))
                {
                    rootDestFolderName += "/";
                }
                var destFolderName = $"{rootDestFolderName}{destFileInfo.Subfolder}";
                var destFolder = await folderCache.CreateFolder(destList, destFolderName);

                _logger.LogInformation($"Copying {sourceFileToCopy.ServerRelativeFilePath} to {destFolder.ServerRelativeUrl}");

                var destFileName = destFilePathInfo.FileName;
                var retry = true;
                var retryCount = 0; 
                while (retry)
                {
                    var newItemCreateInfo = new FileCreationInformation()
                    {
                        Content = ReadFully(sourceFileStream),
                        Url = destFileName,
                        Overwrite = request.ConflictResolution == ConflictResolution.Replace
                    };
                    var newListItem = destFolder.Files.Add(newItemCreateInfo);
                    newListItem.Update();

                    try
                    {
                        await _clientDest.ExecuteQueryAsyncWithThrottleRetries(_logger);
                        retry = false;
                    }
                    catch (ServerException ex) when (ex.Message.Contains("already exists"))
                    {
                        if (request.ConflictResolution == ConflictResolution.NewDesintationName)
                        {
                            retryCount++;
                            retry = true;
                            var fi = new FileInfo(destFileName);

                            // Build new name & try again
                            destFileName = $"{fi.Name.TrimStringFromEnd(fi.Extension)}_{retryCount}{fi.Extension}";
                            _logger.LogWarning($"{fi.Name} already exists. Trying {destFileName}");
                        }
                        else
                        {
                            // Fail
                            throw;
                        }
                    }
                }
            }
        }
    }

    static byte[] ReadFully(Stream input)
    {
        byte[] buffer = new byte[16 * 1024];
        using (var ms = new MemoryStream())
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
    }

}
