using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine.SharePoint;

public class ListCache
{
    private readonly ClientContext _clientContext;
    private readonly ILogger _logger;
    private readonly Action _throttledCallback;
    private readonly Dictionary<string, List> _listCache = new Dictionary<string, List>();

    public ListCache(ClientContext clientContext, ILogger logger, Action throttledCallback)
    {
        _clientContext = clientContext;
        _logger = logger;
        _throttledCallback = throttledCallback;
    }

    public async Task<List> GetByServerRelativeUrl(string url)
    {
        if (!_listCache.ContainsKey(url))
        {
            var list = _clientContext.Web.GetListUsingPath(ResourcePath.FromDecodedUrl(url));
            _clientContext.Load(list);
            _clientContext.Load(list, l => l.RootFolder.ServerRelativeUrl);
            await _clientContext.ExecuteQueryAsyncWithThrottleRetries(_logger, _throttledCallback);
            _listCache.Add(url, list);
        }

        return _listCache[url];
    }
}

public class FolderCache
{
    private readonly ClientContext _clientContext;
    private readonly ILogger _logger;
    private readonly Action _throttledCallback;
    private readonly Dictionary<string, Folder> _folderCache = new Dictionary<string, Folder>();

    public FolderCache(ClientContext clientContext, ILogger logger, Action throttledCallback)
    {
        _clientContext = clientContext;
        _logger = logger;
        _throttledCallback = throttledCallback;
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
        await _clientContext.ExecuteQueryAsyncWithThrottleRetries(_logger, _throttledCallback);

        if (folderUrls.Length > 1)
        {
            var folderPath = string.Join("/", folderUrls, 1, folderUrls.Length - 1);
            return await CreateFolderInternal(curFolder, folderPath);
        }
        return curFolder;
    }
}
