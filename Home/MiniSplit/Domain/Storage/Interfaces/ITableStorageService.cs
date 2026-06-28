using Azure.Data.Tables;
using System.Threading.Tasks;

namespace DomainModels.Storage.Interfaces
{
  public interface ITableStorageService
  {
    Task AddEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity;
    Task<T> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey) 
      where T : class, ITableEntity;
    Task UpdateEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity;
    Task UpsertEntityAsync<T>(string tableName, T entity) where T : class, ITableEntity;

    Task<ITableEntity> GetRecentLogAsync(string tableName, string mode, TimeSpan within);
    Task<bool> WasRecentlyToggledAsync(string tableName, string mode, TimeSpan within);

    Task<IEnumerable<T>> QueryEntitiesAsync<T>(string tableName, string? filter = null)
      where T : class, ITableEntity, new();


  }
}