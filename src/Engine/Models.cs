using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Models
{

    public record StartCopyInfo(string CurrentSite, string FolderToCopy, string DestinationSite, ConflictResolution ConflictResolution);
    public enum ConflictResolution
    {
        FailAction,
        NewDesintationName,
        Replace
    }
}
