using Engine.Configuration;
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
        var tokenManagerDestSite = new SPOTokenManager(_config, batch.Request.DestinationSite, _logger);
        var clientDest = await tokenManagerDestSite.GetOrRefreshContext();

        var app = await AuthUtils.GetNewClientApp(_config);
        var downloader = new SharePointFileDownloader(app, _config, _logger);

        foreach (var fileToCopy in batch.Files)
        {
            using (var sourceFileStream = await downloader.DownloadAsStream(fileToCopy))
            {
                var destFileInfo = fileToCopy.From(batch.Request);
                var thisFileInfo = ServerRelativeFilePathInfo.FromServerRelativeFilePath(destFileInfo.ServerRelativeFilePath);

                var list = clientDest.Web.GetListUsingPath(ResourcePath.FromDecodedUrl(destFileInfo.List.ServerRelativeUrl));
                clientDest.Load(list);
                await clientDest.ExecuteQueryAsyncWithThrottleRetries(_logger);

                var folder = await CreateFolder(list, thisFileInfo.FolderPath, clientDest);

                var newItemCreateInfo = new FileCreationInformation()
                {
                    Content = ReadFully(sourceFileStream),
                    Url = thisFileInfo.FileName,
                };
                var oListItem = folder.Files.Add(newItemCreateInfo);
                oListItem.Update();

                await clientDest.ExecuteQueryAsyncWithThrottleRetries(_logger);

            }
        }
    }

    static byte[] ReadFully(Stream input)
    {
        byte[] buffer = new byte[16 * 1024];
        using (MemoryStream ms = new MemoryStream())
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Create Folder (including nested) client object
    /// </summary>
    public async Task<Folder> CreateFolder(List list, string fullFolderPath, ClientContext clientContext)
    {
        return await CreateFolderInternal(list.RootFolder, fullFolderPath, clientContext);
    }

    private async Task<Folder> CreateFolderInternal(Folder parentFolder, string fullFolderPath, ClientContext clientContext)
    {
        var folderUrls = fullFolderPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        string folderUrl = folderUrls[0];
        var curFolder = parentFolder.Folders.Add(folderUrl);
        clientContext.Load(curFolder);
        await clientContext.ExecuteQueryAsyncWithThrottleRetries(_logger);

        if (folderUrls.Length > 1)
        {
            var folderPath = string.Join("/", folderUrls, 1, folderUrls.Length - 1);
            return await CreateFolderInternal(curFolder, folderPath, clientContext);
        }
        return curFolder;
    }
}
