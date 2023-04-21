﻿using Engine.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Engine.Utils
{
    /// <summary>
    /// HttpClient that can handle HTTP 429s automatically
    /// </summary>
    public class SecureSPThrottledHttpClient : AutoThrottleHttpClient
    {
        public SecureSPThrottledHttpClient(Config config, bool ignoreRetryHeader, ILogger debugTracer) : base(ignoreRetryHeader, debugTracer, new SecureSPHandler(config))
        {
        }
    }

    public class SecureSPHandler : DelegatingHandler
    {
        protected Config _config;
        private AuthenticationResult? auth = null;
        public SecureSPHandler(Config config)
        {
            _config = config;
            InnerHandler = new HttpClientHandler();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {

            // Get auth for REST
            var app = await AuthUtils.GetNewClientApp(_config);

            if (auth == null || auth.ExpiresOn < DateTimeOffset.Now.AddMinutes(5))
            {
                auth = await app.AuthForSharePointOnline(_config.BaseServerAddress);
            }
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            return await base.SendAsync(request, cancellationToken);
        }

    }

}
