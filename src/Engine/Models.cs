namespace Engine.Models;

/// <summary>
/// From Flow inputs
/// </summary>
public record StartCopyRequest(string CurrentWebUrl, string RelativeUrlToCopy, string DestinationWebUrl, string RelativeUrlDestination, ConflictResolution ConflictResolution)
{
    public bool IsValid => !string.IsNullOrEmpty(CurrentWebUrl) && !string.IsNullOrEmpty(RelativeUrlToCopy) && !string.IsNullOrEmpty(DestinationWebUrl) && !string.IsNullOrEmpty(RelativeUrlDestination);
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

