using Azure;
using Azure.Data.Tables;
using DomainModels.Storage.Interfaces;
using Microsoft.Extensions.Logging;

namespace BackendServices
{
  public class TableStorageService : ITableStorageService
  {
    private readonly TableServiceClient _tableServiceClient;
    private readonly ILogger<TableStorageService> _logger;

    public TableStorageService(string connectionString, ILogger<TableStorageService> logger)
    {
      _tableServiceClient = new TableServiceClient(connectionString);
      _logger = logger;
    }

    public async Task AddEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity
    {
      try
      {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync();
        await tableClient.AddEntityAsync(entity);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to add entity to table {TableName}. Entity: {@Entity}", tableName, entity);
        throw;
      }
    }

    public async Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity
    {
      try
      {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
        return response.Value;
      }
      catch (RequestFailedException ex) when (ex.Status == 404)
      {
        _logger.LogWarning("Entity not found in table {TableName} with PK: {PartitionKey}, RK: {RowKey}", tableName, partitionKey, rowKey);
        return null;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get entity from table {TableName} with PK: {PartitionKey}, RK: {RowKey}", tableName, partitionKey, rowKey);
        throw;
      }
    }

    public async Task UpdateEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity
    {
      try
      {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        await tableClient.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to update entity in table {TableName}. Entity: {@Entity}", tableName, entity);
        throw;
      }
    }

    public async Task UpsertEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity
    {
      try
      {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        await tableClient.CreateIfNotExistsAsync();
        await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to upsert entity in table {TableName}. Entity: {@Entity}", tableName, entity);
        throw;
      }
    }

    public async Task<ITableEntity?> GetRecentLogAsync(string tableName, string mode, TimeSpan within)
    {
      try
      {
        var tableClient = _tableServiceClient.GetTableClient(tableName);

        // Calculate the lower bound for the timestamp
        var since = DateTimeOffset.UtcNow - within;

        // Build the filter for Mode and TimestampUtc
        string filter = TableClient.CreateQueryFilter<MiniSplitLogActivity>(
            e => e.Mode == mode && e.TimestampUtc >= since
        );

        // Query the table for matching logs
        var results = new List<MiniSplitLogActivity>();
        await foreach (var entity in tableClient.QueryAsync<MiniSplitLogActivity>(filter))
        {
          results.Add(entity);
        }

        // Return the most recent log (by TimestampUtc), or null if none found
        return results
            .OrderByDescending(e => e.TimestampUtc)
            .FirstOrDefault();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to get recent log from table {TableName} for mode {Mode} within {Within}", tableName, mode, within);
        throw;
      }
    }

    protected async Task<bool> RecentlyToggledAsync(string mode, TimeSpan window)
    {
      var recentLogs = await GetRecentLogAsync("minisplitlogs", mode, window);
      if (recentLogs == null) return false;

      _logger.LogInformation($"Last log was {recentLogs.Timestamp}, within window? {recentLogs.Timestamp >= DateTime.UtcNow.Subtract(window)}");

      return recentLogs.Timestamp >= DateTime.UtcNow.Subtract(window);
    }

    public async Task<bool> WasRecentlyToggledAsync(string tableName, string mode, TimeSpan within)
    {
      var recent = await GetRecentLogAsync(tableName, mode, within);
      return recent != null && recent.Timestamp >= DateTime.UtcNow.Subtract(within);
    }

    public async Task<IEnumerable<T>> QueryEntitiesAsync<T>(string tableName, string? filter = null)
      where T : class, ITableEntity, new()
    {
      try
      {
        var tableClient = _tableServiceClient.GetTableClient(tableName);
        var results = new List<T>();

        await foreach (var entity in tableClient.QueryAsync<T>(filter))
        {
          results.Add(entity);
        }

        return results;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to query entities from table {TableName} with filter {Filter}", tableName, filter);
        throw;
      }
    }



  }
}
