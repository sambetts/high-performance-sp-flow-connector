using Azure.Messaging.ServiceBus;
using Engine.Configuration;
using Engine.Core;
using Engine.Models;
using Microsoft.Extensions.Logging;
using PnP.Core.Model.SharePoint;
using PnP.Core.Services;

namespace Engine;

public class SharePointAndServiceBusFileResultManager : IFileResultManager
{
    private ILogger _logger;
    private readonly PnPContext _clientSourceContext;
    private ServiceBusSender _serviceBusSender;
    public SharePointAndServiceBusFileResultManager(Config config, ILogger logger, PnPContext clientSourceContext)
    {
        _logger = logger;
        _clientSourceContext = clientSourceContext;
        var client = new ServiceBusClient(config.ConnectionStrings.ServiceBus);
        _serviceBusSender = client.CreateSender(config.QueueNameOperations);
    }

    public async Task ProcessLargeFiles(FileCopyBatch fileCopyBatch)
    {
        var m = new ServiceBusMessage(fileCopyBatch.ToJson());
        await _serviceBusSender.SendMessageAsync(m);
        _logger.LogInformation($"Sent {fileCopyBatch.Files.Count} file references to service bus to copy");
    }

    public async Task ProcessRootFiles(BaseItemsCopyBatch batch)
    {
        var urlDest = _clientSourceContext.Web.Url + batch.Request.RelativeUrlDestination;

        _logger.LogInformation($"Copying {batch.FilesAndDirs.Count} files/dirs to {urlDest}");   

        // https://pnp.github.io/pnpcore/using-the-sdk/sites-copymovecontent.html
        var copyJobs = await _clientSourceContext.Site.CreateCopyJobsAsync(batch.FilesAndDirs.ToArray(),
            urlDest, new CopyMigrationOptions
            {
                AllowSchemaMismatch = true,
                AllowSmallerVersionLimitOnDestination = true,
                IgnoreVersionHistory = true,
                // Note: set IsMoveMode = true to move the file(s)
                IsMoveMode = false,
                BypassSharedLock = true,
                ExcludeChildren = true,
                NameConflictBehavior = batch.Request.ConflictResolution == ConflictResolution.Replace ? SPMigrationNameConflictBehavior.Replace :
                    batch.Request.ConflictResolution == ConflictResolution.NewDesintationName ? SPMigrationNameConflictBehavior.KeepBoth : SPMigrationNameConflictBehavior.Fail,
            });

        _logger.LogInformation($"Waiting for {copyJobs.Count} copy jobs to finish");    

        await _clientSourceContext.Site.EnsureCopyJobHasFinishedAsync(copyJobs);
        _logger.LogInformation($"Finished copying {batch.FilesAndDirs.Count} files/dirs to {urlDest}");
    }
}
