using Engine.Core;
using Engine.SharePoint;
using Microsoft.Extensions.Logging;

namespace Engine.Code;

/// <summary>
/// Finds files in a SharePoint site collection. Uses generic paging token type to support abstract paging mechanisms.
/// </summary>
public class DataCrawler<PAGETOKENTYPE>
{
    private readonly ILogger _tracer;

    public DataCrawler(ILogger tracer)
    {
        _tracer = tracer;
    }

    public async Task<DocLibCrawlContents> CrawlListAllPages(IListLoader<PAGETOKENTYPE> listLoader, string fromPath)
    {
        DocLibCrawlContentsPageResponse<PAGETOKENTYPE>? listPage = null;

        var listResultsAll = new DocLibCrawlContents();
        PAGETOKENTYPE? token = default;

        var allFolders = new List<string>();

        int pageCount = 1;
        while (listPage == null || listPage.NextPageToken != null)
        {
            listPage = await listLoader.GetListItemsPage(token, fromPath);
            token = listPage.NextPageToken;

            foreach (var file in listPage.FilesFound)
            {
                listResultsAll.FilesFound.Add(file);
            }
            _tracer.LogInformation($"Loaded {listPage.FilesFound.Count.ToString("N0")} files and {listPage.FoldersFound.Count.ToString("N0")} folders from list '{listPage.ListLoaded.Title}' on page {pageCount}...");

            allFolders.AddRange(listPage.FoldersFound);

            pageCount++;
        }
        if (pageCount > 1)
        {
            _tracer.LogInformation($"List '{listPage.ListLoaded.Title}' totals: {listResultsAll.FilesFound.Count.ToString("N0")} files in scope and {listResultsAll.FoldersFound.Count.ToString("N0")} folders");
        }

        // Add unique folders
        listResultsAll.FoldersFound.AddRange(allFolders.Where(newFolderFound => !listResultsAll.FoldersFound.Contains(newFolderFound)));
        return listResultsAll;
    }
}
