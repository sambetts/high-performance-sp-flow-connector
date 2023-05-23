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
    Task<string> ProcessFile(SharePointFileInfoWithList sourceFileToCopy, StartCopyRequest request);
}

public interface IFileResultManager
{
    Task ProcessChunk(FileCopyBatch fileCopyBatch);
}
