﻿using Engine.Configuration;
using Engine.Core;
using Engine.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine.SharePoint;

public class SharePointFileListProcessor : IFileListProcessor
{
    private readonly ILogger _logger;
    private readonly Config _config;

    public SharePointFileListProcessor(Config config, ILogger logger)
    {
        _logger = logger;
        _config = config;
    }
    public async Task CopyToDestination(FileCopyBatch batch)
    {
        var tokenManagerDestSite = new SPOTokenManager(_config, batch.Request.DestinationWebUrl, _logger);
        var clientDest = await tokenManagerDestSite.GetOrRefreshContext();

        var app = await AuthUtils.GetNewClientApp(_config);
        var downloader = new SharePointFileDownloader(app, _config, _logger);

        await CopyFiles(downloader, clientDest, batch.Files, batch.Request);
        
    }

    private async Task CopyFiles(SharePointFileDownloader downloader, ClientContext clientDest, List<SharePointFileInfoWithList> files, Models.StartCopyRequest request)
    {
        var listCache = new ListCache(clientDest, _logger);
        var folderCache = new FolderCache(clientDest, _logger);
        foreach (var fileToCopy in files)
        {
            using (var sourceFileStream = await downloader.DownloadAsStream(fileToCopy))
            {
                var destFileInfo = fileToCopy.ConvertFromForSameSiteCollection(request);
                var thisFileInfo = ServerRelativeFilePathInfo.FromServerRelativeFilePath(destFileInfo.ServerRelativeFilePath);

                var list = await listCache.GetByServerRelativeUrl(destFileInfo.List.ServerRelativeUrl);

                var rootFolderName = thisFileInfo.FolderPath.TrimStringFromStart(list.RootFolder.ServerRelativeUrl);
                var folder = await folderCache.CreateFolder(list, rootFolderName);

                _logger.LogInformation($"Copying {fileToCopy.ServerRelativeFilePath} to {folder.ServerRelativeUrl}");

                var fileName = thisFileInfo.FileName;
                var retry = true;
                var retryCount = 0; 
                while (retry)
                {
                    var newItemCreateInfo = new FileCreationInformation()
                    {
                        Content = ReadFully(sourceFileStream),
                        Url = fileName,
                        Overwrite = request.ConflictResolution == Models.ConflictResolution.Replace
                    };
                    var newListItem = folder.Files.Add(newItemCreateInfo);
                    newListItem.Update();

                    try
                    {
                        await clientDest.ExecuteQueryAsyncWithThrottleRetries(_logger);
                        retry = false;
                    }
                    catch (ServerException ex) when (ex.Message.Contains("already exists"))
                    {
                        if (request.ConflictResolution == Models.ConflictResolution.NewDesintationName)
                        {
                            retryCount++;
                            retry = true;
                            var fi = new FileInfo(fileName);

                            // Build new name & try again
                            fileName = $"{fi.Name.TrimStringFromEnd(fi.Extension)}_{retryCount}{fi.Extension}";
                            _logger.LogWarning($"{fi.Name} already exists. Trying {fileName}");
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
