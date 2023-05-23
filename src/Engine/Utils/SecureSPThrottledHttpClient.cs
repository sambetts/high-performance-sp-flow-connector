using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

namespace Engine.Utils;

/// <summary>
/// HttpClient that can handle HTTP 429s automatically
/// </summary>
public class SecureSPThrottledHttpClient : AutoThrottleHttpClient
{
    public SecureSPThrottledHttpClient(AuthenticationResult authentication, bool ignoreRetryHeader, ILogger debugTracer) 
        : base(ignoreRetryHeader, debugTracer, new SecureSPHandler(authentication))
    {
    }
}

public class SecureSPHandler : DelegatingHandler
{
    private readonly AuthenticationResult _authentication;
    public SecureSPHandler(AuthenticationResult authentication)
    {
        InnerHandler = new HttpClientHandler();
        _authentication = authentication;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authentication.AccessToken);

        return await base.SendAsync(request, cancellationToken);
    }

}
