using Azure;
using Azure.Data.Tables;

namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class MiniSplitRuntimeEntity : ITableEntity
  {
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public bool? IsOn { get; set; }
    public string? Mode { get; set; }
    public string? Notes { get; set; }

    public DateTimeOffset? StartTime { get; set; }
    public DateTimeOffset? EndTime { get; set; }
    public string? Duration { get; set; } // Optional string form


  }
}
