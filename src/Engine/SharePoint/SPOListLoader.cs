using Engine.Core;
using Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SharePoint.Client;

namespace Engine.SharePoint;

/// <summary>
/// Loads a SharePoint document library using the SharePoint CSOM API. Supports paging.
/// </summary>
public class SPOListLoader : IListLoader<ListItemCollectionPosition>
{
    private List? _listDef = null;
    private readonly Guid _listId;
    private readonly SPOTokenManager _tokenManager;
    private readonly ILogger _logger;

    public SPOListLoader(Guid listId, SPOTokenManager tokenManager, ILogger logger)
    {
        _listId = listId;
        _tokenManager = tokenManager;
        _logger = logger;
    }

    public async static Task<Guid> GetListId(CopyInfo sourceInfo, ClientContext spClient, ILogger logger)
    {
        var sourceList = spClient.Web.GetListUsingPath(ResourcePath.FromDecodedUrl(sourceInfo.ListUrl));
        try
        {
            spClient.Load(sourceList, l => l.Id, l => l.Title);
            await spClient.ExecuteQueryAsyncWithThrottleRetries(logger);
        }
        catch (System.Net.WebException ex)
        {
            logger.LogError($"Got exception '{ex.Message}' loading data for source list URL '{sourceInfo.ListUrl}'.");
            throw;
        }

        return sourceList.Id;
    }

    public async Task<DocLibCrawlContentsPageResponse<ListItemCollectionPosition>> GetListItemsPage(ListItemCollectionPosition? position)
    {
        SiteList? listModel = null;
        var pageResults = new DocLibCrawlContentsPageResponse<ListItemCollectionPosition>();

        // List get-all query, RecursiveAll
        var camlQuery = new CamlQuery();
        camlQuery.ViewXml = "<View Scope=\"RecursiveAll\"><Query>" +
            "<OrderBy><FieldRef Name='ID' Ascending='TRUE'/></OrderBy></Query>" +
            "<RowLimit Paged=\"TRUE\">5000</RowLimit>" +
            "</View>";

        // Large-list support & paging
        ListItemCollection listItems = null!;
        camlQuery.ListItemCollectionPosition = position;

        // For large lists, make sure we refresh the context when the token expires. Do it for each page.
        var spClientList = await _tokenManager.GetOrRefreshContext(() => _listDef = null);      // When token expires, clear list def so it's reloaded

        // Load list definition if needed
        if (_listDef == null)
        {
            _listDef = spClientList.Web.Lists.GetById(_listId);
            spClientList.Load(_listDef, l => l.BaseType, l => l.ItemCount, l => l.RootFolder, list => list.Title);
            await spClientList.ExecuteQueryAsyncWithThrottleRetries(_logger);
        }
        pageResults.ListLoaded = new SiteList { Title = _listDef.Title, ServerRelativeUrl = _listDef.RootFolder.ServerRelativeUrl };

        // List items
        listItems = _listDef.GetItems(camlQuery);
        spClientList.Load(listItems, l => l.ListItemCollectionPosition);

        if (_listDef.BaseType == BaseType.DocumentLibrary)
        {
            // Load docs
            spClientList.Load(listItems,
                             items => items.Include(
                                item => item.Id,
                                item => item.FileSystemObjectType,
                                item => item["Modified"],
                                item => item["Editor"],
                                item => item["File_x0020_Size"],
                                item => item.File.Exists,
                                item => item.File.ServerRelativeUrl,
                                item => item.File.VroomItemID,
                                item => item.File.VroomDriveID
                            )
                        );

            // Set drive ID when 1st results come back
            listModel = new DocLib()
            {
                Title = _listDef.Title,
                ServerRelativeUrl = _listDef.RootFolder.ServerRelativeUrl
            };
        }
        else
        {
            // Unsupported list type
            return pageResults;
        }

        try
        {
            await spClientList.ExecuteQueryAsyncWithThrottleRetries(_logger);
        }
        catch (System.Net.WebException ex)
        {
            _logger.LogError($"Got error reading list: {ex.Message}.");
        }

        // Remember position, if more than 5000 items are in the list
        pageResults.NextPageToken = listItems.ListItemCollectionPosition;

        foreach (var item in listItems)
        {
            var contentTypeId = item.FieldValues["ContentTypeId"]?.ToString();
            var itemIsFolder = contentTypeId != null && contentTypeId.StartsWith("0x012");
            var itemUrl = item.FieldValues["FileRef"]?.ToString();

            if (!itemIsFolder)
            {
                SharePointFileInfoWithList? foundFileInfo = null;

                // We might be able get the drive Id from the actual list, but not sure how...get it from 1st item instead
                var docLib = (DocLib)listModel;
                if (string.IsNullOrEmpty(docLib.DriveId))
                {
                    try
                    {
                        ((DocLib)listModel).DriveId = item.File.VroomDriveID;
                    }
                    catch (ServerObjectNullReferenceException)
                    {
                        _logger.LogWarning($"WARNING: Couldn't get Drive info for list {_listDef.Title} on item {itemUrl}. Ignoring.");
                        break;
                    }
                }

                foundFileInfo = ProcessDocLibItem(item, listModel, spClientList);
                if (foundFileInfo != null)
                {
                    pageResults.FilesFound.Add(foundFileInfo!);
                }
            }
            else
            {
                pageResults.FoldersFound.Add(itemUrl!);
            }
        }

        return pageResults;
    }

    /// <summary>
    /// Process a single document library item.
    /// </summary>
    private SharePointFileInfoWithList? ProcessDocLibItem(ListItem docListItem, SiteList listModel, ClientContext spClient)
    {
        if (docListItem.FileSystemObjectType == FileSystemObjectType.File && docListItem.File.Exists)
        {
            var foundFileInfo = GetSharePointFileInfo(docListItem, docListItem.File.ServerRelativeUrl, listModel, spClient);
            return foundFileInfo;
        }

        return null;
    }

    SharePointFileInfoWithList GetSharePointFileInfo(ListItem item, string url, SiteList listModel, ClientContext _spClient)
    {
        var dir = "";
        if (item.FieldValues.ContainsKey("FileDirRef"))
        {
            dir = item.FieldValues["FileDirRef"].ToString();
            if (dir!.StartsWith(listModel.ServerRelativeUrl))
            {
                // Truncate list URL from dir value of item
                dir = dir.Substring(listModel.ServerRelativeUrl.Length).TrimStart("/".ToCharArray());
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(item), "Can't find dir column");
        }

        var dt = DateTime.MinValue;
        if (DateTime.TryParse(item.FieldValues["Modified"]?.ToString(), out dt))
        {
            var authorFieldObj = item.FieldValues["Editor"];
            if (authorFieldObj != null)
            {
                var authorVal = (FieldUserValue)authorFieldObj;
                var author = !string.IsNullOrEmpty(authorVal.Email) ? authorVal.Email : authorVal.LookupValue;
                var isGraphDriveItem = listModel is DocLib;
                long size = 0;

                // Doc or list-item?
                if (!isGraphDriveItem)
                {
                    var sizeVal = item.FieldValues["SMTotalFileStreamSize"];

                    if (sizeVal != null)
                        long.TryParse(sizeVal.ToString(), out size);

                    // No Graph IDs - probably a list item
                    return new SharePointFileInfoWithList
                    {
                        Author = author,
                        ServerRelativeFilePath = url,
                        LastModified = dt,
                        WebUrl = _spClient.Web.Url,
                        SiteUrl = _spClient.Site.Url,
                        Subfolder = dir.TrimEnd("/".ToCharArray()),
                        List = listModel,
                        FileSize = size
                    };
                }
                else
                {
                    var sizeVal = item.FieldValues["File_x0020_Size"];

                    if (sizeVal != null)
                        long.TryParse(sizeVal.ToString(), out size);
                    return new DriveItemSharePointFileInfo
                    {
                        Author = author,
                        ServerRelativeFilePath = url,
                        LastModified = dt,
                        WebUrl = _spClient.Web.Url,
                        SiteUrl = _spClient.Site.Url,
                        Subfolder = dir.TrimEnd("/".ToCharArray()),
                        GraphItemId = item.File.VroomItemID,
                        DriveId = item.File.VroomDriveID,
                        List = listModel,
                        FileSize = size
                    };
                }
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(item), "Can't find author column");
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(item), "Can't find modified column");
        }
    }
}
