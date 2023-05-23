using Azure.Messaging.ServiceBus;
using Engine.Configuration;
using Engine.Core;
using Microsoft.Extensions.Logging;

namespace Engine;

public class SBFileResultManager : IFileResultManager
{
    private Config _config;
    private ILogger _logger;
    private ServiceBusSender _serviceBusSender;
    public SBFileResultManager(Config config, ILogger logger)
    {
        _config = config;
        _logger = logger;

        var client = new ServiceBusClient(config.ConnectionStrings.ServiceBus);
        _serviceBusSender = client.CreateSender(config.QueueNameOperations);
    }

    public async Task ProcessChunk(FileCopyBatch fileCopyBatch)
    {
        var m = new ServiceBusMessage(fileCopyBatch.ToJson());
        await _serviceBusSender.SendMessageAsync(m);
        _logger.LogInformation($"Sent {fileCopyBatch.Files.Count} file references to service bus to copy");
    }
}
