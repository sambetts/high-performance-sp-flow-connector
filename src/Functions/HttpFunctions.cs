using System.Net;
using System.Text.Json;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Engine.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using PnP.Core.Services;

namespace Functions;

public class HttpFunctions
{
    const string TASK_ID_PARAM_NAME = "taskId";
    private readonly ILogger<HttpFunctions> _logger;
    private readonly Config _config;
    private readonly IPnPContextFactory _contextFactory;
    private readonly TaskQueueManager _taskQueueManager;

    public HttpFunctions(ILoggerFactory loggerFactory, Config config, IPnPContextFactory contextFactory, TaskQueueManager taskQueueManager)
    {
        _logger = loggerFactory.CreateLogger<HttpFunctions>();
        _config = config;
        _contextFactory = contextFactory;
        _taskQueueManager = taskQueueManager;
    }

    [Function(nameof(CheckMigration))]
    public async Task<HttpResponseData> CheckMigration([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var idStr = query[TASK_ID_PARAM_NAME];
        if (!string.IsNullOrEmpty(idStr))
        {
            var id = Guid.Empty;
            if (Guid.TryParse(idStr, out id))
            { 
                var task = _taskQueueManager.GetTask(id);
                if (task.IsCompletedSuccessfully)
                {
                    return req.CreateResponse(HttpStatusCode.OK);
                }
                else if (task.IsFaulted)
                {
                    var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                    if (task.Exception != null)
                    {
                        await response.WriteStringAsync(task.Exception.ToString());
                    }
                    else
                    {
                        await response.WriteStringAsync("Unknown error");
                    }
                    return response;
                }
                else if (!task.IsCompleted)
                {
                    return ReturnWorkingOnIt(req, id);
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await response.WriteStringAsync("Unknown task state");
                    
                    return response;
                }
            }
        }

        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    [Function("FlowReceiver")]
    public async HttpResponseData StartMigration([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
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
            var m = new SharePointFileMigrationManager<HttpFunctions>(_config, _logger);
            var migrationId = await m.SendCopyJobToSB(flowData);

            // Add job to service bus

            var newtaskId = _taskQueueManager.AddNew(migrationTask);

            return ReturnWorkingOnIt(req, newtaskId);
        }
        else
        {
            _logger.LogWarning($"Got invalid Json: '{bodyStr}'");
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            return response;
        }
    }

    // https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-create-api-app#perform-long-running-tasks-with-the-polling-action-pattern
    HttpResponseData ReturnWorkingOnIt(HttpRequestData req, Guid taskId)
    {
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        var checkUrl = $"{_config.BaseFunctionsAppAddress}/api/{nameof(CheckMigration)}?{TASK_ID_PARAM_NAME}={taskId}";

        response.Headers.Add("location", checkUrl);
        response.Headers.Add("retry-after", "5");

        return response;
    }
}
