using Engine.Models;
using Engine.Utils;
using System.Text.Json.Serialization;

namespace Engine.SharePoint;

public class SiteList : IEquatable<SiteList>
{
    public SiteList() { }

    public string Title { get; set; } = string.Empty;
    public string ServerRelativeUrl { get; set; } = string.Empty;

    public bool Equals(SiteList? other)
    {
        if (other == null) return false;
        return ServerRelativeUrl == other.ServerRelativeUrl && Title == other.Title;
    }
}

public class DocLib : SiteList
{
    public DocLib() { }
    public string DriveId { get; set; } = string.Empty;
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

    /// <summary>
    /// Parent list
    /// </summary>
    public SiteList List { get; set; } = new SiteList();

    public bool ValidFor(StartCopyRequest copyCfg)
    {
        if (copyCfg is null)
        {
            throw new ArgumentNullException(nameof(copyCfg));
        }

        var fullSourceUrl = copyCfg.CurrentSite + copyCfg.RelativeUrlToCopy;

        if (!this.FullSharePointUrl.StartsWith(fullSourceUrl))
        {
            return false;
        }
        return true;
    }

    public SharePointFileInfoWithList From(StartCopyRequest copyCfg)
    {
        if (copyCfg is null)
        {
            throw new ArgumentNullException(nameof(copyCfg));
        }

        if (!ValidFor(copyCfg))
        {
            throw new ArgumentOutOfRangeException(nameof(copyCfg), "Out of scope");
        }

        var thisFileInfo = ServerRelativeFilePathInfo.FromServerRelativeFilePath(ServerRelativeFilePath);

        var url = copyCfg.RelativeUrlToCopy;
        if (!url.EndsWith("/"))
        {
            url += "/";
        }
        var sourceFileName = url + thisFileInfo.FileName;
        var siteRootMinusFilePath = ServerRelativeFilePath.TrimStringFromEnd(sourceFileName);
        var destinationServerRelativeFilePath = $"{siteRootMinusFilePath}{copyCfg.RelativeUrlDestination}/{thisFileInfo.FileName}";


        var newCopyFile = new SharePointFileInfoWithList
        {
            SiteUrl = this.SiteUrl,
            WebUrl = this.WebUrl,
            ServerRelativeFilePath = destinationServerRelativeFilePath,
            Author = this.Author,
            Subfolder = this.Subfolder,
            LastModified = this.LastModified,
            FileSize = this.FileSize,
            List = this.List
        };

        if (!newCopyFile.FullSharePointUrl.StartsWith(copyCfg.DestinationSite))
        {
            throw new ArgumentOutOfRangeException($"This file {FullSharePointUrl} does not exist in source {copyCfg.CurrentSite}");
        }

        return newCopyFile;
    }
}

public class ServerRelativeFilePathInfo
{
    public static ServerRelativeFilePathInfo FromServerRelativeFilePath(string serverRelativeFilePath)
    {
        if (string.IsNullOrEmpty(serverRelativeFilePath))
        {
            throw new ArgumentException($"'{nameof(serverRelativeFilePath)}' cannot be null or empty.", nameof(serverRelativeFilePath));
        }
        if (!serverRelativeFilePath.EndsWith("/"))
        {
            var lastSlash = serverRelativeFilePath.LastIndexOf('/');
            if (lastSlash != -1)
            {
                var i = new ServerRelativeFilePathInfo();
                i.FolderPath = serverRelativeFilePath.Substring(0, lastSlash);
                i.FileName = serverRelativeFilePath.Substring(lastSlash + 1, serverRelativeFilePath.Length - lastSlash - 1);

                return i;
            }
        }

        throw new ArgumentOutOfRangeException($"Invalid {serverRelativeFilePath}");
    }

    public string FileName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
}

public class DriveItemSharePointFileInfo : SharePointFileInfoWithList
{
    public DriveItemSharePointFileInfo() : base() { }

    public string DriveId { get; set; } = string.Empty;
    public string GraphItemId { get; set; } = string.Empty;
}


public class DocLibCrawlContentsPageResponse<PAGETOKENTYPE> : DocLibCrawlContents
{
    public PAGETOKENTYPE? NextPageToken { get; set; } = default(PAGETOKENTYPE);
    public SiteList ListLoaded { get; set; } = null!;
}

public class DocLibCrawlContents
{
    public List<SharePointFileInfoWithList> FilesFound { get; set; } = new();

    public List<string> FoldersFound { get; set; } = new();
}
