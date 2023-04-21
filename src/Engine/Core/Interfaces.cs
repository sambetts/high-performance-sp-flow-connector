using Engine.SharePoint;

namespace Engine.Core;

public interface IListLoader<PAGETOKENTYPE>
{
    public Task<DocLibCrawlContentsPageResponse<PAGETOKENTYPE>> GetListItemsPage(PAGETOKENTYPE? token);

}
public interface IFileListProcessor
{
    Task Copy(FileCopyBatch batch);
}

public interface IFileResultManager
{
    Task ProcessChunk(FileCopyBatch fileCopyBatch);
}
