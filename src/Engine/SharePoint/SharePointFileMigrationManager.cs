using Engine.Configuration;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.SharePoint;

public class SharePointFileMigrationManager : FileMigrationManager
{
    private readonly Config _config;
    private readonly SPOTokenManager _tokenManager;
    public SharePointFileMigrationManager(string siteUrl, Config config, ILogger logger) : base(logger)
    {
        _config = config;
        _tokenManager = new SPOTokenManager(_config, siteUrl, _logger);
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopyAndSendToServiceBus(StartCopyRequest startCopyInfo)
    {
        var spClient = await _tokenManager.GetOrRefreshContext();
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentSite, startCopyInfo.RelativeUrlToCopy);

        var guid = await SPOListLoader.GetListId(sourceInfo, spClient, _logger);

        var sbSend = new SBFileResultManager(_config, _logger);

        return await base.StartCopy(startCopyInfo, new SPOListLoader(guid, _tokenManager, _logger), sbSend);
    }


    public async Task MakeCopy(FileCopyBatch batch)
    {
        await MakeCopy(batch, new SharePointFileListProcessor(_config, _logger));
    }
}
