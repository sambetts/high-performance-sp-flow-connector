using Engine;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Engine.Utils;

namespace UnitTests;

[TestClass]
public class ModelTests
{
    
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
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction)));

        // Same site but different folder
        Assert.IsFalse(file1.ValidFor(new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/subfolder",
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction)));
    }

    [TestMethod]
    public void SharePointFileInfoWithListCopyTests()
    {
        // Valid test
        var copyCfg1 = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/",
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction);

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

        var file2 = file1.From(copyCfg1);
        Assert.IsTrue(file2.FullSharePointUrl == "https://m365x72460609.sharepoint.com/sites/Files/Shared Documents/FlowCopy/Contoso.pptx");


        var copyCfgNoTrailingSlash = new StartCopyRequest("https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents",
                       "https://m365x72460609.sharepoint.com/sites/Files", "/Shared Documents/FlowCopy", ConflictResolution.FailAction);
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

        var file2NoTrailingSlash = file1NoTrailingSlash.From(copyCfgNoTrailingSlash);
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

        Assert.ThrowsException<ArgumentOutOfRangeException>(() => fileOutOfScope.From(copyCfg1));
    }

}
