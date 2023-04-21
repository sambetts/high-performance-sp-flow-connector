namespace Engine.Models;

/// <summary>
/// From Flow inputs
/// </summary>
public record StartCopyRequest(string CurrentSite, string RelativeUrlToCopy, string DestinationSite, string RelativeUrlDestination, ConflictResolution ConflictResolution);

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

