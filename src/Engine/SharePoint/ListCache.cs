using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine.SharePoint;

public class ListCache
{
    private readonly ClientContext _clientContext;
    private readonly ILogger _logger;

    private readonly Dictionary<string, List> _listCache = new Dictionary<string, List>();

    public ListCache(ClientContext clientContext, ILogger logger)
    {
        _clientContext = clientContext;
        _logger = logger;
    }

    public async Task<List> GetByServerRelativeUrl(string url)
    {
        if (!_listCache.ContainsKey(url))
        {
            var list = _clientContext.Web.GetListUsingPath(ResourcePath.FromDecodedUrl(url));
            _clientContext.Load(list);
            _clientContext.Load(list, l => l.RootFolder.ServerRelativeUrl);
            await _clientContext.ExecuteQueryAsyncWithThrottleRetries(_logger);
            _listCache.Add(url, list);
        }

        return _listCache[url];
    }
}

public class FolderCache
{
    private readonly ClientContext _clientContext;
    private readonly ILogger _logger;

    private readonly Dictionary<string, Folder> _folderCache = new Dictionary<string, Folder>();

    public FolderCache(ClientContext clientContext, ILogger logger)
    {
        _clientContext = clientContext;
        _logger = logger;
    }

    public async Task<Folder> GetByServerRelativeUrl(List list, string rootFolderName)
    {
        if (!_folderCache.ContainsKey(rootFolderName))
        {
            var folder = await CreateFolder(list, rootFolderName);

        }

        return _folderCache[rootFolderName];
    }


    /// <summary>
    /// Create Folder (including nested) client object
    /// </summary>
    public async Task<Folder> CreateFolder(List list, string fullFolderPath)
    {
        return await CreateFolderInternal(list.RootFolder, fullFolderPath);
    }

    private async Task<Folder> CreateFolderInternal(Folder parentFolder, string fullFolderPath)
    {
        var folderUrls = fullFolderPath.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        string folderUrl = folderUrls[0];
        var curFolder = parentFolder.Folders.Add(folderUrl);
        _clientContext.Load(curFolder);
        await _clientContext.ExecuteQueryAsyncWithThrottleRetries(_logger);

        if (folderUrls.Length > 1)
        {
            var folderPath = string.Join("/", folderUrls, 1, folderUrls.Length - 1);
            return await CreateFolderInternal(curFolder, folderPath);
        }
        return curFolder;
    }
}
