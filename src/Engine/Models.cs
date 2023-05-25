using Engine.SharePoint;
using System.Text.Json.Serialization;

namespace Engine.Models;

/// <summary>
/// From Flow inputs
/// </summary>
public record StartCopyRequest(string CurrentWebUrl, string RelativeUrlToCopy, string DestinationWebUrl, string RelativeUrlDestination, ConflictResolution ConflictResolution, bool DeleteAfterCopy)
{
    public bool IsValid => !string.IsNullOrEmpty(CurrentWebUrl) && !string.IsNullOrEmpty(RelativeUrlToCopy) && !string.IsNullOrEmpty(DestinationWebUrl) && !string.IsNullOrEmpty(RelativeUrlDestination);
}

public class AsyncStartCopyRequest
{
    public AsyncStartCopyRequest(StartCopyRequest startCopyRequest, string requestId)
    {
        StartCopyRequest = startCopyRequest;
        RequestId = requestId;
    }

    [JsonIgnore]
    public bool IsValid => StartCopyRequest != null && !string.IsNullOrEmpty(RequestId);

    public StartCopyRequest StartCopyRequest { get; set; }
    public string RequestId { get; set; }
}

/// <summary>
/// Data CSOM can use
/// </summary>
public class CopyInfo
{
    public CopyInfo(string siteUrl, string relativeUrl)
    {
        if (string.IsNullOrEmpty(siteUrl))
        {
            throw new ArgumentNullException(nameof(siteUrl));
        }
        if (string.IsNullOrEmpty(relativeUrl) || !relativeUrl.StartsWith("/"))
        {
            throw new ArgumentNullException(nameof(relativeUrl));
        }

        var folders = relativeUrl.Split("/", StringSplitOptions.RemoveEmptyEntries);
        if (folders.Length == 0)
        {
            throw new ArgumentNullException(nameof(relativeUrl));
        }

        this.ListUrl = $"{siteUrl}/{folders[0]}";
        this.FoldersRelativePath = string.Join("/", folders.Skip(1));
    }

    public string ListUrl { get; set; }
    public string FoldersRelativePath { get; set; }
}
public enum ConflictResolution
{
    FailAction,
    NewDesintationName,
    Replace
}

public abstract class BaseClass
{
    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this);
    }
}

public abstract class BaseCopyBatch : BaseClass
{
    public StartCopyRequest Request { get; set; } = null!;

    [JsonIgnore]
    public virtual bool IsValid => Request != null && Request.IsValid;

}

public class FileCopyBatch : BaseCopyBatch
{
    public List<SharePointFileInfoWithList> Files { get; set; } = new();

    [JsonIgnore]
    public override bool IsValid => Files.Count > 0 && base.IsValid;

}

public class BaseItemsCopyBatch : BaseCopyBatch
{
    public List<string> FilesAndDirs { get; set; } = new();

    [JsonInclude]
    public override bool IsValid => FilesAndDirs.Count > 0 && base.IsValid;

}
