namespace Engine.Models
{
    /// <summary>
    /// From Flow inputs
    /// </summary>
    public record StartCopyRequest(string CurrentSite, string RelativeUrlToCopy, string DestinationSite, ConflictResolution ConflictResolution);

    /// <summary>
    /// Data CSOM can use
    /// </summary>
    public class CopyInfo
    {
        public CopyInfo(StartCopyRequest startCopyInfo)
        {
            if (startCopyInfo is null)
            {
                throw new ArgumentNullException(nameof(startCopyInfo));
            }
            if (string.IsNullOrEmpty(startCopyInfo.RelativeUrlToCopy) || !startCopyInfo.RelativeUrlToCopy.StartsWith("/"))
            {
                throw new ArgumentNullException(nameof(startCopyInfo.RelativeUrlToCopy));
            }

            var folders = startCopyInfo.RelativeUrlToCopy.Split("/", StringSplitOptions.RemoveEmptyEntries);
            if (folders.Length == 0)
            {
                throw new ArgumentNullException(nameof(startCopyInfo.RelativeUrlToCopy));
            }

            this.ListUrl = $"{startCopyInfo.CurrentSite}/{folders[0]}";
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
}
