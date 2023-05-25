using System.Net;
using System.Text.Json;
using Engine;
using Engine.Configuration;
using Engine.Models;
using Engine.SharePoint;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Functions;

public class HttpFunctions
{
    const string TASK_ID_PARAM_NAME = "taskId";
    private readonly ILogger<HttpFunctions> _logger;
    private readonly Config _config;
    private readonly IAzureStorageManager _azureStorageManager;
    private readonly SharePointFileMigrationManager _fileMigrationManager;

    public HttpFunctions(ILoggerFactory loggerFactory, Config config, IAzureStorageManager azureStorageManager, SharePointFileMigrationManager fileMigrationManager)
    {
        _logger = loggerFactory.CreateLogger<HttpFunctions>();
        _config = config;
        _azureStorageManager = azureStorageManager;
        _fileMigrationManager = fileMigrationManager;
    }

    /// <summary>
    /// This function is used to check the status of a migration task. Called by Flow/Logic apps to check on status
    /// </summary>
    [Function(nameof(CheckMigration))]
    public async Task<HttpResponseData> CheckMigration([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var migrationId = query[TASK_ID_PARAM_NAME];
        if (!string.IsNullOrEmpty(migrationId))
        {
            var migrationStatus = await _azureStorageManager.GetMigrationStatus(migrationId);

            if (migrationStatus != null)
            {
                if (migrationStatus.Finished.HasValue && migrationStatus.Error == null)
                {
                    _logger.LogInformation($"Migration {migrationId} finished successfully");
                    return req.CreateResponse(HttpStatusCode.OK);
                }
                else if (migrationStatus.Error != null)     // Job error
                {
                    var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await response.WriteStringAsync(migrationStatus.Error);
                    
                    return response;
                }
                else if (migrationStatus.Finished == null)  // Still running
                {
                    _logger.LogInformation($"Migration {migrationId} still running");
                    return ReturnWorkingOnIt(req, migrationId);
                }
                else
                {
                    _logger.LogError($"Migration {migrationId} has unknown state");
                    var response = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await response.WriteStringAsync("Unknown task state");

                    return response;
                }
            }
        }

        return req.CreateResponse(HttpStatusCode.BadRequest);
    }

    [Function("FlowReceiver")]
    public async Task<HttpResponseData> StartMigration([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData httpRequest)
    {
        _logger.LogInformation("FlowReceiver HTTP trigger function processed a request.");

        var bodyStr = string.Empty;
        using (var reader = new StreamReader(httpRequest.Body))
        {
            bodyStr = reader.ReadToEnd();
        }

        StartCopyRequest? flowStartCopyData = null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        try
        {
            flowStartCopyData = JsonSerializer.Deserialize<StartCopyRequest>(bodyStr, options);
        }
        catch (JsonException)
        {
            // Ignore
        }
        if (flowStartCopyData != null && flowStartCopyData.IsValid)
        {
            var newAsyncStartCopyRequest = new AsyncStartCopyRequest(flowStartCopyData, Guid.NewGuid().ToString());

            // Add job to service bus
            await _fileMigrationManager.SendCopyJobToServiceBusAndRegisterNewJob(newAsyncStartCopyRequest, _azureStorageManager);

            // Keep the Flow running
            return ReturnWorkingOnIt(httpRequest, newAsyncStartCopyRequest.RequestId);
        }
        else
        {
            _logger.LogWarning($"Got invalid Json from HTTP request: '{bodyStr}'");
            var response = httpRequest.CreateResponse(HttpStatusCode.BadRequest);
            return response;
        }
    }

    // https://learn.microsoft.com/en-us/azure/logic-apps/logic-apps-create-api-app#perform-long-running-tasks-with-the-polling-action-pattern
    HttpResponseData ReturnWorkingOnIt(HttpRequestData req, string taskId)
    {
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        var checkUrl = $"{_config.BaseFunctionsAppAddress}/api/{nameof(CheckMigration)}?{TASK_ID_PARAM_NAME}={taskId}";

        response.Headers.Add("location", checkUrl);
        response.Headers.Add("retry-after", "10");

        return response;
    }
}
