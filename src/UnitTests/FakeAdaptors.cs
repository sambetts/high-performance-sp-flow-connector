using Engine;
using Engine.Core;
using Engine.SharePoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTests;

public class FakeFileListProcessor : IFileListProcessor
{
    public Task Copy(FileCopyBatch batch)
    {
        return Task.CompletedTask;
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

    public Task<DocLibCrawlContentsPageResponse<string>> GetListItemsPage(string? token)
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
