using Engine.Configuration;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.SharePoint;

public class SharePointFileMigrationManager : FileMigrationManager
{
    private readonly Config _config;
    private readonly SPOTokenManager _sourceTokenManager;
    public SharePointFileMigrationManager(string sourceSiteUrl, Config config, ILogger logger) : base(logger)
    {
        _config = config;
        _sourceTokenManager = new SPOTokenManager(_config, sourceSiteUrl, _logger);
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopyAndSendToServiceBus(StartCopyRequest startCopyInfo)
    {
        var spClient = await _sourceTokenManager.GetOrRefreshContext();
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentWebUrl, startCopyInfo.RelativeUrlToCopy);

        var guid = await SPOListLoader.GetListId(sourceInfo, spClient, _logger);

        var sbSend = new SBFileResultManager(_config, _logger);

        return await base.StartCopy(startCopyInfo, new SPOListLoader(guid, _sourceTokenManager, _logger), sbSend);
    }


    public async Task MakeCopy(FileCopyBatch batch)
    {
        await MakeCopy(batch, new SharePointFileListProcessor(_config, _logger));
    }
}
