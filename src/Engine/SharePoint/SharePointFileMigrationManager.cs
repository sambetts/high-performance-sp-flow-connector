using Azure.Messaging.ServiceBus;
using Engine.Configuration;
using Engine.Models;
using Engine.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using PnP.Core.Services;

namespace Engine.SharePoint;

/// <summary>
/// SharePoint specific implementation of the FileMigrationManager
/// </summary>
/// <typeparam name="T">Logging category</typeparam>
public class SharePointFileMigrationManager : FileMigrationManager
{
    private readonly Config _config;

    public SharePointFileMigrationManager(Config config, ILogger<SharePointFileMigrationManager> logger) : base(logger)
    {
        _config = config;
    }

    public async Task<List<SharePointFileInfoWithList>> StartCopyAndSendBigFilesToServiceBus(StartCopyRequest startCopyInfo, IPnPContextFactory contextFactory)
    {
        var sourceTokenManager = new SPOTokenManager(_config, startCopyInfo.CurrentWebUrl, _logger);
        var spClient = await sourceTokenManager.GetOrRefreshContext();
        var sourceInfo = new CopyInfo(startCopyInfo.CurrentWebUrl, startCopyInfo.RelativeUrlToCopy);

        var guid = await SPOListLoader.GetListId(sourceInfo, spClient, _logger);

        using (var context = await contextFactory.CreateAsync(new Uri(startCopyInfo.CurrentWebUrl)))
        {
            var sbSend = new SharePointAndServiceBusFileResultManager(_config, _logger, context);
            return await base.StartCopy(startCopyInfo, new SPOListLoader(guid, sourceTokenManager, _logger), sbSend);
        }
    }

    public async Task SendCopyJobToServiceBusAndRegisterNewJob(AsyncStartCopyRequest startCopyInfo, IAzureStorageManager azureStorageManager)
    {
        var client = new ServiceBusClient(_config.ConnectionStrings.ServiceBus);
        var m = new ServiceBusMessage(System.Text.Json.JsonSerializer.Serialize(startCopyInfo));

        var _serviceBusSender = client.CreateSender(_config.QueueNameOperations);
        await _serviceBusSender.SendMessageAsync(m);
        _logger.LogInformation($"Sent file copy request to service bus to process async");


        await azureStorageManager.SetNewMigrationStatus(startCopyInfo.RequestId, null, false);
    }

    public async Task CompleteCopyToSharePoint(FileCopyBatch batch, AuthenticationResult authentication, ClientContext clientContext)
    {
        await base.CompleteCopy(batch, new SharePointFileListProcessor(_config, _logger, authentication, clientContext));
    }

    public async Task<bool> CheckDestinationAndSourceExist(StartCopyRequest flowStartCopyData)
    {
        // Check webs
        if (!(flowStartCopyData.CurrentWebUrl.StartsWith(_config.BaseSPOAddress) && flowStartCopyData.DestinationWebUrl.StartsWith(_config.BaseSPOAddress)))
        {
            return false;
        }
        var baseUrlSource = flowStartCopyData.CurrentWebUrl.TrimStringFromStart(_config.BaseSPOAddress);
        var baseUrlDest = flowStartCopyData.DestinationWebUrl.TrimStringFromStart(_config.BaseSPOAddress);

        var sourceTokenManager = new SPOTokenManager(_config, flowStartCopyData.CurrentWebUrl, _logger);
        var sourceClient = await sourceTokenManager.GetOrRefreshContext();
        var sourceFolderExists = await CheckExists(baseUrlSource + flowStartCopyData.RelativeUrlToCopy, sourceClient);

        var destTokenManager = new SPOTokenManager(_config, flowStartCopyData.CurrentWebUrl, _logger);
        var destClient = await destTokenManager.GetOrRefreshContext();

        var destFolderExists = await CheckExists(baseUrlDest + flowStartCopyData.RelativeUrlDestination, destClient);

        return sourceFolderExists && destFolderExists;
    }

    private async Task<bool> CheckExists(string relativeUrlToCopy, ClientContext sourceClient)
    {
        var f = sourceClient.Web.GetFolderByServerRelativePath(ResourcePath.FromDecodedUrl(relativeUrlToCopy));
        sourceClient.Load(f);

        try
        {
            await sourceClient.ExecuteQueryAsyncWithThrottleRetries(_logger);
        }
        catch (ServerException ex) when (ex.Message.Contains("File Not Found"))
        {
            return false;
        }
        return true;
    }
}
