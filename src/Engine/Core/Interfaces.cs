using Engine.Models;
using Engine.SharePoint;

namespace Engine.Core;

public interface IListLoader<PAGETOKENTYPE>
{
    public Task<DocLibCrawlContentsPageResponse<PAGETOKENTYPE>> GetListItemsPage(PAGETOKENTYPE? token, string fromPath);

}
public interface IFileListProcessor
{
    Task Init();
    Task<SingleFileFileUploadResults> ProcessFile(SharePointFileInfoWithList sourceFileToCopy, StartCopyRequest request);
}

public interface IFileResultManager
{
    Task ProcessLargeFiles(FileCopyBatch fileCopyBatch);
    Task ProcessRootFiles(BaseItemsCopyBatch absoluteUrls);
}
