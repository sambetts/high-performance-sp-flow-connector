using System.Text.Json;
using Engine;
using Engine.SharePoint;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Functions;

public class SBFunctions
{
    private readonly ILogger _logger;
    private readonly SharePointFileMigrationManager<SBFunctions> _fileMigrationManager;

    public SBFunctions(ILoggerFactory loggerFactory, SharePointFileMigrationManager<SBFunctions> fileMigrationManager)
    {
        _logger = loggerFactory.CreateLogger<SBFunctions>();
        _fileMigrationManager = fileMigrationManager;
    }

    [Function(nameof(ProcessFileOperation))]
    public async Task ProcessFileOperation([ServiceBusTrigger("operations", Connection = "ServiceBus")] string messageContents)
    {

        if (string.IsNullOrEmpty(messageContents))
        {
            _logger.LogWarning("Got empty message from the queue. Ignoring");
            return;
        }

        FileCopyBatch? update = null;
        try
        {
            update = JsonSerializer.Deserialize<FileCopyBatch>(messageContents);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message, ex);
        }

        if (update != null && update.IsValid)
        {
            await _fileMigrationManager.MakeCopy(update);
        }
        else
        {
            _logger.LogWarning($"Invalid message received from service-bus: '{messageContents}'");
        }
    }
}
