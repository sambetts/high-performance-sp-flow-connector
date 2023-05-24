using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;

namespace UnitTests;

[TestClass]
public class ModelTests
{
    [TestMethod]
    public void DocLibCrawlContentsExtensionsTests()
    {
        var list1 = new SiteList { ServerRelativeUrl = "/list1", Title = "List 1" };
        var d = new DocLibCrawlContents
        {
            FilesFound = new List<SharePointFileInfoWithList>
            {
                new SharePointFileInfoWithList 
                { 
                    List = list1, 
                    SiteUrl = "https://m365x352268.sharepoint.com/sites",
                    WebUrl = "https://m365x352268.sharepoint.com/sites/Files/subsite",
                    ServerRelativeFilePath = "/sites/Files/subsite/Shared Documents/Contoso.pptx", 
                    FileSize = 1 
                },
                new SharePointFileInfoWithList 
                { 
                    List = list1,
                    SiteUrl = "https://m365x352268.sharepoint.com/sites",
                    WebUrl = "https://m365x352268.sharepoint.com/sites/Files/subsite",
                    ServerRelativeFilePath = "/sites/Files/subsite/Shared Documents/subfolder/Contoso.mp4", 
                    FileSize = 2147483648 
                },   // 2gb + 1 byte
            },
            FoldersFound = new List<FolderInfo> 
            { 
                new FolderInfo
                { 
                    FolderPath = "Root folder",
                    ServerRelativeFilePath = "/sites/Files/subsite/Shared Documents/Root folder",
                    WebUrl = "https://m365x352268.sharepoint.com/sites/Files/subsite",
                },
                new FolderInfo
                { 
                    FolderPath = "Root folder/subfolder1",
                    ServerRelativeFilePath = "/sites/Files/subsite/Shared Documents/Root folder/subfolder1",
                    WebUrl = "https://m365x352268.sharepoint.com/sites/Files/subsite",
                }
            },
        };

        var largeFiles = d.FilesFound.GetLargeFiles();
        Assert.IsTrue(largeFiles.Count == 1);
        Assert.IsTrue(largeFiles[0].FullSharePointUrl == "https://m365x352268.sharepoint.com/sites/Files/subsite/Shared Documents/subfolder/Contoso.mp4");

        var filesAndFolders = d.GetRootFilesAndFoldersBelowTwoGig();    
        Assert.IsTrue(filesAndFolders.Count == 2);
        Assert.IsTrue(filesAndFolders.Contains("https://m365x352268.sharepoint.com/sites/Files/subsite/Shared Documents/Contoso.pptx"));
        Assert.IsTrue(filesAndFolders.Contains("https://m365x352268.sharepoint.com/sites/Files/subsite/Shared Documents/Root folder"));
    }

    [TestMethod]
    public void TrimStringFromStart()
    {
        var s1 = "string and that";
        const string EXTRA = " plus some extra bits";
        var s2 = s1 + EXTRA;
        var trimmed = s2.TrimStringFromStart(s1);

        Assert.IsTrue(trimmed == EXTRA);

        Assert.ThrowsException<ArgumentException>(() => "randoString".TrimStringFromStart(EXTRA));
    }


    [TestMethod]
    public void TrimStringFromEnd()
    {
        var stringFirstBit = "string and that";
        const string EXTRA = " plus some extra bits";
        var stringWithExtra = stringFirstBit + EXTRA;
        var trimmed = stringWithExtra.TrimStringFromEnd(EXTRA);

        Assert.IsTrue(trimmed == stringFirstBit);

        Assert.ThrowsException<ArgumentException>(() => "randoString".TrimStringFromStart(EXTRA));
    }


    [TestMethod]
    public void FromServerRelativeFilePathTests()
    {
        var i = ServerRelativeFilePathInfo.FromServerRelativeFilePath("/sites/Files/Shared Documents/Contoso.pptx");
        Assert.IsTrue(i.FileName == "Contoso.pptx");
        Assert.IsTrue(i.FolderPath == "/sites/Files/Shared Documents");

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ServerRelativeFilePathInfo.FromServerRelativeFilePath("Contoso.pptx"));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ServerRelativeFilePathInfo.FromServerRelativeFilePath("/"));
    }

    [TestMethod]
    public void SharePointFileInfoWithListValidForTests()
    {
        var file1 = new SharePointFileInfoWithList
        {
            List = new SiteList { ServerRelativeUrl = "/list1" },
            Author = "Whoever",
            FileSize = 100,
            LastModified = DateTime.UtcNow,
            SiteUrl = "https://m365x72460609.sharepoint.com/sites/Files/",
            WebUrl = "https://m365x72460609.sharepoint.com/sites/Files",
            ServerRelativeFilePath = "/sites/Files/Shared Documents/Contoso.pptx"
        };

        Assert.IsTrue(file1.ValidFor(new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/",
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction, false)));

        // Same site but different folder
        Assert.IsFalse(file1.ValidFor(new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/subfolder",
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction, false)));
    }

    [TestMethod]
    public void ConvertFromForSameSiteCollectionTests()
    {
        // Valid test
        var copyCfgDifferentList = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/Source",
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Docs/FlowCopy", ConflictResolution.FailAction, false);

        var sourceFile = new SharePointFileInfoWithList
        {
            List = new SiteList { ServerRelativeUrl = "/Shared Documents" },
            Author = "Whoever",
            FileSize = 100,
            LastModified = DateTime.UtcNow,
            SiteUrl = "https://m365x72460609.sharepoint.com/sites/Files/",
            WebUrl = "https://m365x72460609.sharepoint.com/sites/Files",
            ServerRelativeFilePath = "/sites/Files/Shared Documents/Source/Contoso.pptx"
        };

        var fileInDifferentList = sourceFile.ConvertFromForSameSiteCollection(copyCfgDifferentList);
        Assert.IsTrue(fileInDifferentList.FullSharePointUrl == "https://m365x72460609.sharepoint.com/sites/Files/Docs/FlowCopy/Contoso.pptx");
        Assert.IsTrue(fileInDifferentList.List.ServerRelativeUrl == "/sites/Files/Docs");


        var copyCfgNoTrailingSlash = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents",
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction, false);
        var file1NoTrailingSlash = new SharePointFileInfoWithList
        {
            List = new SiteList { ServerRelativeUrl = "/list1" },
            Author = "Whoever",
            FileSize = 100,
            LastModified = DateTime.UtcNow,
            SiteUrl = "https://m365x72460609.sharepoint.com/sites/Files",
            WebUrl = "https://m365x72460609.sharepoint.com/sites/Files",
            ServerRelativeFilePath = "/sites/Files/Shared Documents/Contoso.pptx"
        };

        var file2NoTrailingSlash = file1NoTrailingSlash.ConvertFromForSameSiteCollection(copyCfgNoTrailingSlash);
        Assert.IsTrue(file2NoTrailingSlash.FullSharePointUrl == "https://m365x72460609.sharepoint.com/sites/Files/Shared Documents/FlowCopy/Contoso.pptx");


        // Source file from another site
        var fileOutOfScope = new SharePointFileInfoWithList
        {
            List = new SiteList { ServerRelativeUrl = "/list1" },
            Author = "Whoever",
            FileSize = 100,
            LastModified = DateTime.UtcNow,
            SiteUrl = "https://m365x72460609.sharepoint.com/sites/Other",
            WebUrl = "https://m365x72460609.sharepoint.com/sites/Other",
            ServerRelativeFilePath = "/sites/Other/Shared Documents/Contoso.pptx"
        };

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => fileOutOfScope.ConvertFromForSameSiteCollection(copyCfgDifferentList));
    }

}
