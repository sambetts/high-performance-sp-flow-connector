using System.Net;
using System.Text.Json;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PnP.Core.Services;

namespace Functions;

public class HttpFunctions
{
    private readonly ILogger<HttpFunctions> _logger;
    private readonly Config _config;
    private readonly IPnPContextFactory _contextFactory;

    public HttpFunctions(ILoggerFactory loggerFactory, Config config, IPnPContextFactory contextFactory)
    {
        _logger = loggerFactory.CreateLogger<HttpFunctions>();
        _config = config;
        _contextFactory = contextFactory;
    }

    [Function("FlowReceiver")]
    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        _logger.LogInformation("FlowReceiver HTTP trigger function processed a request.");

        var bodyStr = string.Empty;
        using (var reader = new StreamReader(req.Body))
        {
            bodyStr = reader.ReadToEnd();
        }

        StartCopyRequest? flowData = null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        try
        {
            flowData = JsonSerializer.Deserialize<StartCopyRequest>(bodyStr, options);
        }
        catch (JsonException)
        {
            // Ignore
        }
        if (flowData != null && flowData.IsValid)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            var m = new SharePointFileMigrationManager<HttpFunctions>(_config, _logger);
            await m.StartCopyAndSendBigFilesToServiceBus(flowData, _contextFactory);

            return response;
        }
        else
        {
            _logger.LogWarning($"Got invalid Json: '{bodyStr}'");
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            return response;
        }

    }
}
