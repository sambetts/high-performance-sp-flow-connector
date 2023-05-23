using Engine.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace Engine.SharePoint;

public class SharePointFileDownloader 
{
    private readonly ILogger _tracer;
    private readonly SecureSPThrottledHttpClient _client;
    public SharePointFileDownloader(AuthenticationResult authentication, ILogger debugTracer) 
    {
        _tracer = debugTracer;
        _client = new SecureSPThrottledHttpClient(authentication, true, debugTracer);

        var productValue = new ProductInfoHeaderValue("SPOColdStorageMigration", "1.0");
        var commentValue = new ProductInfoHeaderValue("(+https://github.com/sambetts/SPOColdStorage)");

        _client.DefaultRequestHeaders.UserAgent.Add(productValue);
        _client.DefaultRequestHeaders.UserAgent.Add(commentValue);
    }

    public async Task<Stream> DownloadAsStream(BaseSharePointFileInfo sharePointFile)
    {
        _tracer.LogTrace($"Downloading '{sharePointFile.FullSharePointUrl}'...");
        var url = $"{sharePointFile.WebUrl}/_api/web/GetFileByServerRelativeUrl('{sharePointFile.ServerRelativeFilePath}')/OpenBinaryStream";

        // Get response but don't buffer full content (which will buffer overlflow for large files)
        using (var response = await _client.GetAsyncWithThrottleRetries(url, HttpCompletionOption.ResponseHeadersRead, _tracer))
        {
            return await response.Content.ReadAsStreamAsync();
        }
    }
}
