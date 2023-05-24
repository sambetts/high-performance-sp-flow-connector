﻿using Engine.Configuration;
using Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using Microsoft.SharePoint.Client;
using PnP.Core.Services;

namespace Engine.SharePoint;

/// <summary>
/// SharePoint specific implementation of the FileMigrationManager
/// </summary>
/// <typeparam name="T">Logging category</typeparam>
public class SharePointFileMigrationManager<T> : FileMigrationManager
{
    private readonly Config _config;

    public SharePointFileMigrationManager(Config config, ILogger<T> logger) : base(logger)
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
            await context.Web.LoadAsync(p => p.Title);
            Console.WriteLine($"Web title = {context.Web.Title}");
            var sbSend = new SharePointAndServiceBusFileResultManager(_config, _logger, context);
            return await base.StartCopy(startCopyInfo, new SPOListLoader(guid, sourceTokenManager, _logger), sbSend);
        }
    }

    public async Task CompleteCopyToSharePoint(FileCopyBatch batch, AuthenticationResult authentication, ClientContext clientContext)
    {
        await base.CompleteCopy(batch, new SharePointFileListProcessor(_config, _logger, authentication, clientContext));
    }
}
