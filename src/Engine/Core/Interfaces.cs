using Engine.SharePoint;

namespace Engine.Core;

public interface IListLoader<PAGETOKENTYPE>
{
    public Task<DocLibCrawlContentsPageResponse<PAGETOKENTYPE>> GetListItemsPage(PAGETOKENTYPE? token, string fromPath);

}
public interface IFileListProcessor
{
    Task CopyToDestination(FileCopyBatch batch);
}

public interface IFileResultManager
{
    Task ProcessChunk(FileCopyBatch fileCopyBatch);
}
