using Engine.Configuration;
using Engine.Models;
using Microsoft.Extensions.Logging;

namespace Engine.SharePoint
{
    public class SharePointFileMigrationManager : FileMigrationManager
    {
        private readonly Config _config;

        public SharePointFileMigrationManager(Config config, ILogger logger) : base(logger)
        {
            _config = config;
        }

        public async Task<List<SharePointFileInfoWithList>> StartCopyAndSendToServiceBus(StartCopyRequest startCopyInfo)
        {
            var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentSite, _logger);
            var spClient = await sourceTokenManager.GetOrRefreshContext();
            var sourceInfo = new CopyInfo(startCopyInfo.CurrentSite, startCopyInfo.RelativeUrlToCopy);

            var guid = await SPOListLoader.GetListId(sourceInfo, spClient, _logger);

            var sbSend = new SBFileResultManager(_config, _logger);

            return await base.StartCopy(startCopyInfo, new SPOListLoader(guid, sourceTokenManager, _logger));
        }
    }
}
