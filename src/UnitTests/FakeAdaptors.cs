using Engine;
using Engine.Core;
using Engine.Models;
using Engine.SharePoint;

namespace UnitTests;

public class FailConfigurableTimesFileListProcessor : IFileListProcessor
{
    private readonly int _failCount;
    private Dictionary<SharePointFileInfoWithList, int> _filesFailed = new();

    public FailConfigurableTimesFileListProcessor(int failCount)
    {
        _failCount = failCount;
    }

    public Task Init()
    {
        return Task.CompletedTask;  
    }

    public Task<SingleFileFileUploadResults> ProcessFile(SharePointFileInfoWithList sourceFileToCopy, StartCopyRequest request)
    {
        if (_filesFailed.ContainsKey(sourceFileToCopy) && _filesFailed[sourceFileToCopy] == _failCount)
        {
            return Task.FromResult(new SingleFileFileUploadResults { });
        }
        else
        {
            if (!_filesFailed.ContainsKey(sourceFileToCopy))
            {
                _filesFailed.Add(sourceFileToCopy, 1);
            }
            else
            {
                _filesFailed[sourceFileToCopy] = _filesFailed[sourceFileToCopy] + 1;
            }
            throw new Exception($"Failed to copy file {_filesFailed[sourceFileToCopy]} times");
        }
    }
}

public class FakeChunkManager : IFileResultManager
{
    public Task ProcessChunk(FileCopyBatch fileCopyBatch)
    {
        return Task.CompletedTask;
    }
}

public class FakeLoader : IListLoader<string>
{
    public string Title { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public Guid ListId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public Task<DocLibCrawlContentsPageResponse<string>> GetListItemsPage(string? token, string fromPath)
    {
        var list = new SiteList { Title = "Shared Documents" };
        return Task.FromResult(new DocLibCrawlContentsPageResponse<string>
        {
            NextPageToken = null,
            FilesFound = new List<SharePointFileInfoWithList>
            {
                new()
                {
                    List = list,
                     SiteUrl = "https://m365x352268.sharepoint.com/sites/MigrationHost",
                     WebUrl = "https://m365x352268.sharepoint.com/sites/MigrationHost/sub",
                     Subfolder = "subfolder",
                     ServerRelativeFilePath = "/sites/MigrationHost/sub/Shared%20Documents/Contoso.pptx",
                     Author = "John Doe",
                     FileSize = 1234,
                     LastModified = DateTime.Now
                }
            }
        });
    }
}
