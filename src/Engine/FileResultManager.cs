using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Microsoft.Extensions.Logging;

namespace Engine;

public class FileResultManager : IFileResultManager
{
    private Config _config;
    private ILogger _logger;

    public FileResultManager(Config config, ILogger logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task ProcessChunk(FileCopyBatch fileCopyBatch)
    {
    }
}

public interface IFileResultManager
{
    Task ProcessChunk(FileCopyBatch fileCopyBatch);
}
