using System.Text.Json;
using Engine;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

namespace Functions;

public class SBFunctions
{
    private readonly ILogger _logger;
    private readonly SharePointFileMigrationManager<SBFunctions> _fileMigrationManager;
    private readonly Config _config;

    private static AuthenticationResult? _auth = null;
    private static IConfidentialClientApplication? _confidentialClientApplication = null;
    private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

    public SBFunctions(ILoggerFactory loggerFactory, SharePointFileMigrationManager<SBFunctions> fileMigrationManager, Config config)
    {
        _logger = loggerFactory.CreateLogger<SBFunctions>();
        _fileMigrationManager = fileMigrationManager;
        _config = config;
    }

    [Function(nameof(ProcessFileOperation))]
    public async Task ProcessFileOperation([ServiceBusTrigger("%QueueNameOperations%", Connection = "ServiceBus")] string messageContents)
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
            // Ensure we only have one thread at a time trying to get a new token
            await semaphoreSlim.WaitAsync();
            try
            {
                // Cache creds where possible to avoid hitting KV and SPO too often
                if (_confidentialClientApplication == null)
                {
                    _confidentialClientApplication = await AuthUtils.GetNewClientApp(_config);
                }
                if (SPOTokenManager.NeedsRefresh(_auth))
                {
                    _auth = await _confidentialClientApplication.AuthForSharePointOnline(_config.BaseServerAddress);
                }
            }
            finally
            {
                semaphoreSlim.Release();
            }

            // Make the copy
            if (_auth != null)
            {
                await _fileMigrationManager.CompleteCopyToSharePoint(update, _auth, AuthUtils.GetClientContext(update.Request.DestinationWebUrl, _auth));
            }
        }
        else
        {
            _logger.LogWarning($"Invalid message received from service-bus: '{messageContents}'");
        }
    }
}
