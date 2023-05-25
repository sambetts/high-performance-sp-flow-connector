using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DataCollection.Engine.Models;
using System.Collections.Concurrent;
using System.Net;

namespace Engine;

public interface IAzureStorageManager
{
    Task ClearPropertyValue(string property);

    Task CreateOrUpdateUser(UserEntry userEntry);
    Task CreateOrUpdateExportLogEntry(DateTime started, DateTime? finished, string outputFileName, string status, int recordCount);
    Task CreateOrUpdateNotificationLogEntry(NotificationLogEntry entity);
    Task CreateOrUpdateNotificationSettingsEntry(string upn, Dictionary<NotificationChannel, NotificationSchedule> notificationSchedules);

    Task DeleteUserEntry(string upn);

    Task<ExportLogEntry> GetExportLogEntry(DateTime now);
    Task<NotificationLogEntry?> GetLastNotificationForUpnAndType(string upn, NotificationChannel channel, NotificationType type);
    Task<NotificationSettingsEntry?> GetNotificationSettings(string upn);
    Task<PropertyBagEntry?> GetPropertyValue(string property);
    Task<UserEntry?> GetUserEntry(string upn);
    Task<UserEntry?> GetUserEntryByHashedId(string hashedId);

    Task<IAsyncEnumerable<Page<ExportLogEntry>>> ListRecentExportLogEntries(DateTime cutOff);
    Task<IAsyncEnumerable<Page<NotificationLogEntry>>> ListNotificationsForUpn(string upn);

    Task SetPropertyValue(string property, string value);
}

/// <summary>
/// Handles Azure blob & table storage operations
/// </summary>
public class AzureStorageManager : TableStorageManager, IAzureStorageManager
{
    public const string AzureTablePropertyBag = "propertybag";
    public const string AzureTableUser = "user";
    public const string AzureTableExportLog = "exportlog";
    public const string AzureTableNotificationLog = "notificationlog";
    public const string AzureTableNotificationSettings = "notificationsettings";
    public const string BlobContainerName = "exports";

    public const string ServiceBusQueueNameGraphUpdates = "graphupdates";
    public const string ServiceBusQueueNameAsyncTasks = "asynctasks";
    public const string ServiceBusQueueNameSentimentExports = "sentimentexports";
    public const string ServiceBusQueueNameUserExports = "userexports";

    private readonly BlobServiceClient _blobServiceClient;
    private List<string> _blobContainersVerifiedCreated = new();
    public AzureStorageManager(string storageConnectionString) : base(storageConnectionString)
    {
        _blobServiceClient = new BlobServiceClient(storageConnectionString);
    }

    #region PropertyBag

    public async Task<PropertyBagEntry?> GetPropertyValue(string property)
    {
        var tableClient = await GetTableClient(AzureTablePropertyBag);

        var queryResultsFilter = tableClient.QueryAsync<PropertyBagEntry>(f =>
            f.RowKey == property
        );

        // Iterate the <see cref="Pageable"> to access all queried entities.
        await foreach (var qEntity in queryResultsFilter)
        {
            return qEntity;
        }

        // No results
        return null;
    }

    public async Task SetPropertyValue(string property, string value)
    {
        var tableClient = await GetTableClient(AzureTablePropertyBag);

        var entity = new PropertyBagEntry(property, value);

        // Entity doesn't exist in table, so invoking UpsertEntity will simply insert the entity.
        await tableClient.UpsertEntityAsync(entity);
    }

    public async Task ClearPropertyValue(string property)
    {
        var tableClient = await GetTableClient(AzureTablePropertyBag);
        tableClient.DeleteEntity(PropertyBagEntry.PARTITION_NAME, property);
    }

    #endregion

    #region UserEntry

    public async Task<UserEntry?> GetUserEntry(string upn)
    {
        if (string.IsNullOrEmpty(upn))
        {
            throw new ArgumentException($"'{nameof(upn)}' cannot be null or empty.", nameof(upn));
        }

        var tableClient = await GetTableClient(AzureTableUser);
        try
        {
            var response = await tableClient.GetEntityAsync<UserEntry>(upn, UserEntry.DefaultRowKey);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<UserEntry?> GetUserEntryByHashedId(string hashedId)
    {
        var tableClient = await GetTableClient(AzureTableUser);
        var results = tableClient.QueryAsync<UserEntry>(f => f.HashedId == hashedId);

        await foreach (var user in results)
        {
            return user;
        }

        return null;
    }

    public async Task CreateOrUpdateUser(UserEntry userEntry)
    {
        var tableClient = await GetTableClient(AzureTableUser);

        // Entity doesn't exist in table, so invoking UpsertEntity will simply insert the entity.
        await tableClient.UpsertEntityAsync(userEntry);
    }

    public async Task DeleteUserEntry(string upn)
    {
        var tableClient = await GetTableClient(AzureTableUser);
        await tableClient.DeleteEntityAsync(upn, UserEntry.DefaultRowKey);
    }

    #endregion

    #region ExportLogEntry

    public async Task CreateOrUpdateExportLogEntry(DateTime started, DateTime? finished, string outputFileName, string status, int recordCount)
    {
        var tableClient = await GetTableClient(AzureTableExportLog);

        var entity = new ExportLogEntry(started, finished, outputFileName, status, recordCount);
        await tableClient.UpsertEntityAsync(entity);
    }

    public async Task<ExportLogEntry> GetExportLogEntry(DateTime now)
    {
        var tableClient = await GetTableClient(AzureTableExportLog);

        return await tableClient.GetEntityAsync<ExportLogEntry>(ExportLogEntry.GetPartitionKey(now), ExportLogEntry.GetRowKey(now));
    }

    public async Task<IAsyncEnumerable<Page<ExportLogEntry>>> ListRecentExportLogEntries(DateTime cutOff)
    {
        var tableClient = await GetTableClient(AzureTableExportLog);

        var query = tableClient.QueryAsync<ExportLogEntry>(e => e.Started >= cutOff);

        return query.AsPages();
    }

    #endregion

    #region NotificationLogEntry

    public async Task CreateOrUpdateNotificationLogEntry(NotificationLogEntry entity)
    {
        var tableClient = await GetTableClient(AzureTableNotificationLog);

        await tableClient.UpsertEntityAsync(entity);
    }

    public async Task<IAsyncEnumerable<Page<NotificationLogEntry>>> ListNotificationsForUpn(string upn)
    {
        var tableClient = await GetTableClient(AzureTableNotificationLog);

        var query = tableClient.QueryAsync<NotificationLogEntry>(f => f.PartitionKey == upn);

        return query.AsPages();
    }

    public async Task<NotificationLogEntry?> GetLastNotificationForUpnAndType(string upn, NotificationChannel channel, NotificationType type)
    {
        var pages = await ListNotificationsForUpn(upn);

        await foreach (var page in pages)
        {
            foreach (var notification in page.Values)
            {
                if (notification.NotificationType == type && notification.Channel == channel)
                    return notification;
            }
        }

        return null;
    }

    #endregion

    #region NotificationSettingsEntry

    public async Task CreateOrUpdateNotificationSettingsEntry(string upn, Dictionary<NotificationChannel, NotificationSchedule> notificationSchedules)
    {
        var tableClient = await GetTableClient(AzureTableNotificationSettings);

        var entity = new NotificationSettingsEntry(upn, notificationSchedules);
        await tableClient.UpsertEntityAsync(entity);
    }

    public async Task<NotificationSettingsEntry?> GetNotificationSettings(string upn)
    {
        var tableClient = await GetTableClient(AzureTableNotificationSettings);

        try
        {
            var response = await tableClient.GetEntityAsync<NotificationSettingsEntry>(upn, NotificationSettingsEntry.DefaultRowKey);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    #endregion

    internal async Task<BlobContentInfo> UploadFileBlob(string baseFileName, Stream stream)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(BlobContainerName);
        if (!_blobContainersVerifiedCreated.Contains(BlobContainerName))
        {
            await containerClient.CreateIfNotExistsAsync();
            _blobContainersVerifiedCreated.Add(BlobContainerName);
        }

        var fileRef = containerClient.GetBlobClient(baseFileName);
        var result = await fileRef.UploadAsync(stream, overwrite: true);
        return result.Value;
    }

}

public abstract class TableStorageManager
{
    private readonly TableServiceClient _tableServiceClient;
    private ConcurrentDictionary<string, TableClient> _tableClientCache = new();
    public TableStorageManager(string storageConnectionString)
    {
        _tableServiceClient = new TableServiceClient(storageConnectionString);
    }

    protected async Task<TableClient> GetTableClient(string tableName)
    {
        if (_tableClientCache.TryGetValue(tableName, out var tableClient))
            return tableClient;

        try
        {
            await _tableServiceClient.CreateTableIfNotExistsAsync(tableName);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == "TableAlreadyExists")
        {
            // Supposedly CreateTableIfNotExistsAsync should silently fail if already exists, but this doesn't seem to happen
        }

        tableClient = _tableServiceClient.GetTableClient(tableName);

        _tableClientCache[tableName] = tableClient;

        return tableClient;
    }
}