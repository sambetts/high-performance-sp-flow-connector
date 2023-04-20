using Microsoft.Extensions.Logging;

namespace Engine.SharePoint
{
    /// <summary>
    /// Finds files in a SharePoint site collection
    /// </summary>
    public class SiteListsAndLibrariesCrawler<T>
    {
        #region Constructors & Privates

        private readonly ILogger _tracer;

        public SiteListsAndLibrariesCrawler(ILogger tracer)
        {
            this._tracer = tracer;
        }

        #endregion

        public async Task<SiteCrawlContentsAndStats> CrawlList(IListLoader<T> parentList, Func<SharePointFileInfoWithList, Task>? foundFileCallback)
        {
            PageResponse<T>? listPage = null;

            var listResultsAll = new SiteCrawlContentsAndStats();
            T? token = default(T);

            var allFolders = new List<string>();

            int pageCount = 1;
            while (listPage == null || listPage.NextPageToken != null)
            {
                listPage = await parentList.GetListItems(token);
                token = listPage.NextPageToken;

                foreach (var file in listPage.FilesFound)
                {
                    listResultsAll.FilesFound.Add(file);
                }
                _tracer.LogInformation($"Loaded {listPage.FilesFound.Count.ToString("N0")} files and {listPage.FoldersFound.Count.ToString("N0")} folders from list '{parentList.Title}' on page {pageCount}...");

                allFolders.AddRange(listPage.FoldersFound);

                pageCount++;
            }
            if (pageCount > 1)
            {
                _tracer.LogInformation($"List '{parentList.Title}' totals: {listResultsAll.FilesFound.Count.ToString("N0")} files in scope, " +
                    $"{listResultsAll.IgnoredFiles.ToString("N0")} files ignored, and {listResultsAll.FoldersFound.Count.ToString("N0")} folders");
            }


            // Add unique folders
            listResultsAll.FoldersFound.AddRange(allFolders.Where(newFolderFound => !listResultsAll.FoldersFound.Contains(newFolderFound)));

            return listResultsAll;

        }
    }

}
