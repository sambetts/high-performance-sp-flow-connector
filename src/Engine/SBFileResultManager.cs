using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Engine.Configuration;
using Engine.Core;
using Engine.Models;
using Engine.SharePoint;
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
        _serviceBusSender = client.CreateSender("<QUEUE-NAME>");
    }

    public async Task ProcessChunk(FileCopyBatch fileCopyBatch)
    {
    }
}
