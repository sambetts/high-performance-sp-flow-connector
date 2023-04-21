using Engine.Configuration;
using Engine.Core;
using Engine.Utils;
using Microsoft.Extensions.Logging;

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
    public async Task Copy(FileCopyBatch batch)
    {
        var tokenManagerSourceSite = new SPOTokenManager(_config, batch.Request.CurrentSite, _logger);
        var clientSource = await tokenManagerSourceSite.GetOrRefreshContext();
        var tokenManagerDestSite = new SPOTokenManager(_config, batch.Request.CurrentSite, _logger);
        var clientDest = await tokenManagerDestSite.GetOrRefreshContext();

        var app = await AuthUtils.GetNewClientApp(_config);
        var downloader = new SharePointFileDownloader(app, _config, _logger);

        foreach (var fileToCopy in batch.Files)
        {
            using (var sourceFileStream = await downloader.DownloadAsStream(fileToCopy))
            {
                clientDest
            }
        }
    }
}
