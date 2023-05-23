using Engine.Configuration;
using Engine.Core;
using Engine.Models;
using Engine.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;

namespace Engine.SharePoint;

public class SharePointFileListProcessor : IFileListProcessor
{
    private readonly ILogger _logger;
    private readonly ClientContext _clientDest;
    private readonly Config _config;

    private IConfidentialClientApplication? _application;
    private SharePointFileDownloader? _sharePointFileDownloader;
    private ListCache? _listCache;
    private FolderCache? _folderCache;  

    public SharePointFileListProcessor(Config config, ILogger logger, ClientContext clientDest)
    {
        _logger = logger;
        _clientDest = clientDest;
        _config = config;
    }
    public async Task Init()
    {
        _application = await AuthUtils.GetNewClientApp(_config);
        _sharePointFileDownloader = new SharePointFileDownloader(_application, _config, _logger);
        _listCache = new ListCache(_clientDest, _logger);
        _folderCache = new FolderCache(_clientDest, _logger);
    }

    public async Task<string> ProcessFile(SharePointFileInfoWithList sourceFileToCopy, StartCopyRequest request)
    {
        if (_sharePointFileDownloader == null || _listCache == null || _folderCache == null)
        {
            throw new InvalidOperationException("File processor not initialised");
        }
        using (var sourceFileStream = await _sharePointFileDownloader.DownloadAsStream(sourceFileToCopy))
        {
            var destFileInfo = sourceFileToCopy.ConvertFromForSameSiteCollection(request);
            var destFilePathInfo = ServerRelativeFilePathInfo.FromServerRelativeFilePath(destFileInfo.ServerRelativeFilePath);

            var destList = await _listCache.GetByServerRelativeUrl(destFileInfo.List.ServerRelativeUrl);

            // Figure out target folder name & create it if needed
            var rootDestFolderName = destFilePathInfo.FolderPath.TrimStringFromStart(destList.RootFolder.ServerRelativeUrl);
            if (!rootDestFolderName.EndsWith("/"))
            {
                rootDestFolderName += "/";
            }
            var destFolderName = $"{rootDestFolderName}{destFileInfo.Subfolder}";
            var destFolder = await _folderCache.CreateFolder(destList, destFolderName);

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
            return destFileName;
        }
    }

    static byte[] ReadFully(Stream input)
    {
        var buffer = new byte[16 * 1024];
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
