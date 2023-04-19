using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Engine.SharePoint;

public class SiteList : IEquatable<SiteList>
{
    public SiteList() { }
    public SiteList(SiteList l)
    {
        this.Title = l.Title;
        this.ServerRelativeUrl = l.ServerRelativeUrl;
    }

    public string Title { get; set; } = string.Empty;
    public string ServerRelativeUrl { get; set; } = string.Empty;
    public List<BaseSharePointFileInfo> Files { get; set; } = new List<BaseSharePointFileInfo>();

    public bool Equals(SiteList? other)
    {
        if (other == null) return false;
        return ServerRelativeUrl == other.ServerRelativeUrl && Title == other.Title;
    }
}

public class DocLib : SiteList
{
    public DocLib() { }
    public DocLib(SiteList l) : base(l)
    {
        if (l is DocLib)
        {
            var lib = (DocLib)l;
            this.DriveId = lib.DriveId;
            this.Delta = lib.Delta;
            this.Files = lib.Files;
        }
    }
    public string DriveId { get; set; } = string.Empty;

    public List<DocumentSiteWithMetadata> Documents => Files.Where(f => f.GetType() == typeof(DocumentSiteWithMetadata)).Cast<DocumentSiteWithMetadata>().ToList();
    public string Delta { get; set; } = string.Empty;
}
/// <summary>
/// SharePoint Online file metadata for base file-type
/// </summary>
public class BaseSharePointFileInfo
{
    public BaseSharePointFileInfo() { }
    public BaseSharePointFileInfo(BaseSharePointFileInfo driveArg) : this()
    {
        this.SiteUrl = driveArg.SiteUrl;
        this.WebUrl = driveArg.WebUrl;
        this.ServerRelativeFilePath = driveArg.ServerRelativeFilePath;
        this.Author = driveArg.Author;
        this.Subfolder = driveArg.Subfolder;
        this.LastModified = driveArg.LastModified;
        this.FileSize = driveArg.FileSize;
    }

    /// <summary>
    /// Example: https://m365x352268.sharepoint.com/sites/MigrationHost
    /// </summary>
    public string SiteUrl { get; set; } = string.Empty;

    /// <summary>
    /// Example: https://m365x352268.sharepoint.com/sites/MigrationHost/subsite
    /// </summary>
    public string WebUrl { get; set; } = string.Empty;

    /// <summary>
    /// Example: /sites/MigrationHost/Shared%20Documents/Contoso.pptx
    /// </summary>
    public string ServerRelativeFilePath { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// Item sub-folder name. Cannot start or end with a slash
    /// </summary>
    public string Subfolder { get; set; } = string.Empty;

    public DateTime LastModified { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Bytes
    /// </summary>
    public long FileSize { get; set; } = 0;

    /// <summary>
    /// Calculated.
    /// </summary>
    [JsonIgnore]
    public bool IsValidInfo => !string.IsNullOrEmpty(ServerRelativeFilePath) &&
        !string.IsNullOrEmpty(SiteUrl) &&
        !string.IsNullOrEmpty(WebUrl) &&
        this.LastModified > DateTime.MinValue &&
        this.WebUrl.StartsWith(this.SiteUrl) &&
        this.FullSharePointUrl.StartsWith(this.WebUrl) &&
        ValidSubFolderIfSpecified;

    bool ValidSubFolderIfSpecified
    {
        get
        {
            if (string.IsNullOrEmpty(Subfolder))
            {
                return true;
            }
            else
            {
                return !Subfolder.StartsWith("/") && !Subfolder.EndsWith("/") && !Subfolder.Contains(@"//");
            }
        }
    }

    public override string ToString()
    {
        return $"{this.ServerRelativeFilePath}";
    }

    /// <summary>
    /// Calculated. Web + file URL, minus overlap, if both are valid.
    /// </summary>
    [JsonIgnore]
    public string FullSharePointUrl
    {
        get
        {
            // Strip out relative web part of file URL
            const string DOMAIN = "sharepoint.com";
            var domainStart = WebUrl.IndexOf(DOMAIN, StringComparison.CurrentCultureIgnoreCase);
            if (domainStart > -1 && ValidSubFolderIfSpecified)      // Basic checks. IsValidInfo uses this prop so can't use that.
            {
                var webMinusServer = WebUrl.Substring(domainStart + DOMAIN.Length, (WebUrl.Length - domainStart) - DOMAIN.Length);

                if (ServerRelativeFilePath.StartsWith(webMinusServer))
                {
                    var filePathWithoutWeb = ServerRelativeFilePath.Substring(webMinusServer.Length, ServerRelativeFilePath.Length - webMinusServer.Length);

                    return WebUrl + filePathWithoutWeb;
                }
                else
                {
                    return ServerRelativeFilePath;
                }
            }
            else
            {
                return ServerRelativeFilePath;
            }
        }
    }
}

public class SharePointFileInfoWithList : BaseSharePointFileInfo
{
    public SharePointFileInfoWithList() { }
    public SharePointFileInfoWithList(DriveItemSharePointFileInfo driveArg) : base(driveArg)
    {
        this.List = driveArg.List;
    }

    /// <summary>
    /// Parent list
    /// </summary>
    public SiteList List { get; set; } = new SiteList();

}

public class DriveItemSharePointFileInfo : SharePointFileInfoWithList
{
    public DriveItemSharePointFileInfo() : base() { }
    public DriveItemSharePointFileInfo(DriveItemSharePointFileInfo driveArg) : base(driveArg)
    {
        this.DriveId = driveArg.DriveId;
        this.GraphItemId = driveArg.GraphItemId;
    }

    public string DriveId { get; set; } = string.Empty;
    public string GraphItemId { get; set; } = string.Empty;
}

public enum SiteFileAnalysisState
{
    Unknown,
    AnalysisPending,
    AnalysisInProgress,
    Complete,
    FatalError,
    TransientError
}

public class DocumentSiteWithMetadata : DriveItemSharePointFileInfo
{
    public DocumentSiteWithMetadata() { }
    public DocumentSiteWithMetadata(DriveItemSharePointFileInfo driveArg) : base(driveArg)
    {
        this.AccessCount = null;
    }

    public SiteFileAnalysisState State { get; set; } = SiteFileAnalysisState.Unknown;

    public int? AccessCount { get; set; } = null;
    public int VersionCount { get; set; }
    public long VersionHistorySize { get; set; }
}


// https://docs.microsoft.com/en-us/graph/api/resources/itemactivitystat?view=graph-rest-1.0
public class ItemAnalyticsRepsonse
{

    [JsonPropertyName("incompleteData")]
    public AnalyticsIncompleteData? IncompleteData { get; set; }

    [JsonPropertyName("access")]
    public AnalyticsItemActionStat? AccessStats { get; set; }

    [JsonPropertyName("startDateTime")]
    public DateTime StartDateTime { get; set; }

    [JsonPropertyName("endDateTime")]
    public DateTime EndDateTime { get; set; }


    public class AnalyticsIncompleteData
    {
        [JsonPropertyName("wasThrottled")]
        public bool WasThrottled { get; set; }

        [JsonPropertyName("resultsPending")]
        public bool ResultsPending { get; set; }

        [JsonPropertyName("notSupported")]
        public bool NotSupported { get; set; }
    }
    public class AnalyticsItemActionStat
    {
        /// <summary>
        /// The number of times the action took place.
        /// </summary>
        [JsonPropertyName("actionCount")]
        public int ActionCount { get; set; } = 0;

        /// <summary>
        /// The number of distinct actors that performed the action.
        /// </summary>
        [JsonPropertyName("actorCount")]
        public int ActorCount { get; set; } = 0;
    }
}
public interface ISiteCollectionLoader<T>
{
    public Task<List<IWebLoader<T>>> GetWebs();
}


public interface IWebLoader<T>
{
    public Task<List<IListLoader<T>>> GetLists();
}

public interface IListLoader<T>
{
    public Task<PageResponse<T>> GetListItems(T? token);

    public string Title { get; set; }
    public Guid ListId { get; set; }
}

public class PageResponse<T> : BaseSiteCrawlContents
{
    public T? NextPageToken { get; set; } = default(T);
}

public class BaseSiteCrawlContents
{
    public List<SharePointFileInfoWithList> FilesFound { get; set; } = new();

    public List<string> FoldersFound { get; set; } = new();
}

public class SiteCrawlContentsAndStats : BaseSiteCrawlContents
{
    public int IgnoredFiles { get; set; } = 0;
}
