﻿using Engine.Configuration;
using Engine.Core;
using Engine.Models;
using Engine.SharePoint;
using Microsoft.Extensions.Logging;

namespace Engine;

public class SBFileResultManager : IFileResultManager
{
    private Config _config;
    private ILogger _logger;

    public SBFileResultManager(Config config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task ProcessChunk(FileCopyBatch fileCopyBatch)
    {
    }
}
