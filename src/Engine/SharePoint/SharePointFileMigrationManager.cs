using Engine.Configuration;
using Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;

namespace Engine.SharePoint;

public class SharePointFileMigrationManager<T> : FileMigrationManager
{
    private readonly Config _config;
    public SharePointFileMigrationManager(Config config, ILogger<T> logger) : base(logger)
    {
        _config = config;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopyAndSendToServiceBus(StartCopyRequest startCopyInfo)
    {
        var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentWebUrl, _logger);
        var spClient = await sourceTokenManager.GetOrRefreshContext();
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentWebUrl, startCopyInfo.RelativeUrlToCopy);

        var guid = await SPOListLoader.GetListId(sourceInfo, spClient, _logger);

        var sbSend = new SBFileResultManager(_config, _logger);

        return await base.StartCopy(startCopyInfo, new SPOListLoader(guid, sourceTokenManager, _logger), sbSend);
    }


    public async Task CompleteCopyToSharePoint(FileCopyBatch batch, AuthenticationResult authentication, ClientContext clientContext)
    {
        await CompleteCopy(batch, new SharePointFileListProcessor(_config, _logger, authentication, clientContext));
    }
}
