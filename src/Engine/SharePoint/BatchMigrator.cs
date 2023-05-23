using Engine.Models;
using PnP.Core.Model.SharePoint;
using PnP.Core.Services;

namespace Engine.SharePoint;

public class BatchMigrator
{
    private readonly PnPContext _clientSourceContext;

    public BatchMigrator(PnPContext clientSourceContext)
    {
        _clientSourceContext = clientSourceContext;
    }

    public async Task CopyToDestination(FileCopyBatch batch, StartCopyRequest request)
    {
        var urlDest = _clientSourceContext.Web.Url + request.RelativeUrlDestination;

        // https://pnp.github.io/pnpcore/using-the-sdk/sites-copymovecontent.html
        var copyJobs = await _clientSourceContext.Site.CreateCopyJobsAsync(batch.Files.Select(f=> f.FullSharePointUrl).ToArray(),
            urlDest, new CopyMigrationOptions
            {
                AllowSchemaMismatch = true,
                AllowSmallerVersionLimitOnDestination = true,
                IgnoreVersionHistory = true,
                // Note: set IsMoveMode = true to move the file(s)
                IsMoveMode = false,
                BypassSharedLock = true,
                ExcludeChildren = true,
                NameConflictBehavior = request.ConflictResolution == ConflictResolution.Replace ? SPMigrationNameConflictBehavior.Replace :
                    request.ConflictResolution == ConflictResolution.NewDesintationName ? SPMigrationNameConflictBehavior.KeepBoth : SPMigrationNameConflictBehavior.Fail,
            });

        foreach (var copyJob in copyJobs)
        {
            var progress = await _clientSourceContext.Site.GetCopyJobProgressAsync(copyJob);

            Console.WriteLine($"Copy job {copyJob.JobId} is {progress.JobState}");
            if (progress.JobState == MigrationJobState.None)
            {
                // The job is done!
            }
            else if (progress.JobState == MigrationJobState.Processing)
            {
                // The job is running
            }
            else
            {
                // The job is queued
            }
            await Task.Delay(1000);
        }
    }
}
