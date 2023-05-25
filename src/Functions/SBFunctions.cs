using System.Text.Json;
using Engine;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using PnP.Core.Services;

namespace Functions;

public class SBFunctions
{
    private readonly ILogger _logger;
    private readonly SharePointFileMigrationManager _fileMigrationManager;
    private readonly Config _config;
    private readonly IPnPContextFactory _contextFactory;
    private readonly IAzureStorageManager _azureStorageManager;
    private static AuthenticationResult? _appAuth = null;
    private static IConfidentialClientApplication? _confidentialClientApplication = null;
    private static SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

    public SBFunctions(ILoggerFactory loggerFactory, SharePointFileMigrationManager fileMigrationManager, Config config, IPnPContextFactory contextFactory, IAzureStorageManager azureStorageManager)
    {
        _logger = loggerFactory.CreateLogger<SBFunctions>();
        _fileMigrationManager = fileMigrationManager;
        _config = config;
        _contextFactory = contextFactory;
        _azureStorageManager = azureStorageManager;
    }

    [Function(nameof(ProcessFileOperation))]
    public async Task ProcessFileOperation([ServiceBusTrigger("%QueueNameOperations%", Connection = "ServiceBus")] string messageContents)
    {

        if (string.IsNullOrEmpty(messageContents))
        {
            _logger.LogWarning("Got empty message from the queue. Ignoring");
            return;
        }

        // We can get a couple of different message types. Todo: improve this. 
        FileCopyBatch? fileCopyRequest = null;
        try
        {
            fileCopyRequest = JsonSerializer.Deserialize<FileCopyBatch>(messageContents);
        }
        catch (JsonException)
        {
            // Ignore
        }

        AsyncStartCopyRequest? startCopyRequest = null;
        try
        {
            startCopyRequest = JsonSerializer.Deserialize<AsyncStartCopyRequest>(messageContents);
        }
        catch (JsonException)
        {
            // Ignore
        }

        if (fileCopyRequest != null && fileCopyRequest.IsValid)
        {
            await RefreshCachedAppAuthIfNeeded();

            // Make the copy
            if (_appAuth != null)
            {
                await _fileMigrationManager.CompleteCopyToSharePoint(fileCopyRequest, _appAuth, AuthUtils.GetClientContext(fileCopyRequest.Request.DestinationWebUrl, _appAuth));
            }
        }
        else if (startCopyRequest != null && startCopyRequest.IsValid)
        {
            await RefreshCachedAppAuthIfNeeded();
            if (_appAuth != null)
            {
                var errorText = string.Empty;
                try
                {
                    await _fileMigrationManager.StartCopyAndSendBigFilesToServiceBus(startCopyRequest.StartCopyRequest, _contextFactory);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error copying files");
                    errorText = ex.Message;
                }

                // Update job status
                if (!string.IsNullOrEmpty(errorText))
                {
                    await _azureStorageManager.SetNewMigrationStatus(startCopyRequest.RequestId, errorText, true);
                }
                else
                {
                    await _azureStorageManager.SetNewMigrationStatus(startCopyRequest.RequestId, null, true);
                }
            }
        }
        else
        {
            _logger.LogWarning($"Invalid message received from service-bus: '{messageContents}'");
        }
    }

    async Task RefreshCachedAppAuthIfNeeded()
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
            if (SPOTokenManager.NeedsRefresh(_appAuth))
            {
                _appAuth = await _confidentialClientApplication.AuthForSharePointOnline(_config.BaseSPOAddress);
            }
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}
