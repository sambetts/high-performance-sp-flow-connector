using Engine.Configuration;
using Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using PnP.Core.Services;

namespace Engine.SharePoint;

public class SharePointFileMigrationManager<T> : FileMigrationManager
{
    private readonly Config _config;
    private readonly IPnPContextFactory _contextFactory;

    public SharePointFileMigrationManager(Config config, ILogger<T> logger, IPnPContextFactory contextFactory) : base(logger)
    {
        _config = config;
        _contextFactory = contextFactory;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopyAndSendToServiceBus(StartCopyRequest startCopyInfo)
    {
        var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentWebUrl, _logger);
        var spClient = await sourceTokenManager.GetOrRefreshContext();
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentWebUrl, startCopyInfo.RelativeUrlToCopy);

        var guid = await SPOListLoader.GetListId(sourceInfo, spClient, _logger);

        using (var context = await _contextFactory.CreateAsync(new Uri("https://contoso.sharepoint.com/sites/hr")))
        {
            await context.Web.LoadAsync(p => p.Title);
            Console.WriteLine($"Web title = {context.Web.Title}");
            var sbSend = new SharePointAndServiceBusFileResultManager(_config, _logger, context);
            return await base.StartCopy(startCopyInfo, new SPOListLoader(guid, sourceTokenManager, _logger), sbSend);
        }

    }


    public async Task CompleteCopyToSharePoint(FileCopyBatch batch, AuthenticationResult authentication, ClientContext clientContext)
    {
        await CompleteCopy(batch, new SharePointFileListProcessor(_config, _logger, authentication, clientContext));
    }
}
