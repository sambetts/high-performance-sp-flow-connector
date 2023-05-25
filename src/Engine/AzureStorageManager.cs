using Azure;
using Azure.Data.Tables;
using System.Collections.Concurrent;

namespace Engine;

public interface IAzureStorageManager
{
    Task ClearMigrationStatus(string migrationId);
    Task<FileMigrationEntry?> GetMigrationStatus(string migrationId);
    Task SetNewMigrationStatus(string migrationId, string? error, bool complete);
}


/// <summary>
/// Handles Azure table storage operations
/// </summary>
public class AzureStorageManager : TableStorageManager, IAzureStorageManager
{
    public const string AzureTableMigrations = "migrations";

    public AzureStorageManager(string storageConnectionString) : base(storageConnectionString)
    {
    }

    public async Task<FileMigrationEntry?> GetMigrationStatus(string migrationId)
    {
        var tableClient = await GetTableClient(AzureTableMigrations);

        var queryResultsFilter = tableClient.QueryAsync<FileMigrationEntry>(f =>
            f.RowKey == migrationId
        );

        // Iterate the <see cref="Pageable"> to access all queried entities.
        await foreach (var qEntity in queryResultsFilter)
        {
            return qEntity;
        }

        // No results
        return null;
    }

    public async Task SetNewMigrationStatus(string migrationId, string? error, bool complete)
    {
        var tableClient = await GetTableClient(AzureTableMigrations);

        var entity = new FileMigrationEntry(migrationId, error, complete);

        // Entity doesn't exist in table, so invoking UpsertEntity will simply insert the entity.
        await tableClient.UpsertEntityAsync(entity);
    }

    public async Task ClearMigrationStatus(string migrationId)
    {
        var tableClient = await GetTableClient(AzureTableMigrations);
        tableClient.DeleteEntity(FileMigrationEntry.PARTITION_NAME, migrationId);
    }
}

public class FileMigrationEntry : ITableEntity
{
    public const string PARTITION_NAME = "Migrations";

    public FileMigrationEntry()
    {
    }

    public FileMigrationEntry(string migrationId, string? error, bool complete)
    {
        PartitionKey = PARTITION_NAME;

        // Key is encoded URL
        RowKey = migrationId;
        Error = error;
        Finished = complete ? DateTime.UtcNow : null;
    }

    public DateTime? Finished { get; set; } = null;
    public string? Error { get; set; } = null;

    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
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