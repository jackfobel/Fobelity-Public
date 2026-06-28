using Azure;
using Azure.Data.Tables;
using System.Text.Json.Serialization;

namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class MiniSplitConfigEntity : ITableEntity
  {
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    [JsonIgnore]
    public ETag ETag { get; set; }

    public int threshhold { get; set; }
    public bool enabled { get; set; }

    public double costPerKWh { get; set; }
    public double kWhPerHour { get; set; }
  }
}
