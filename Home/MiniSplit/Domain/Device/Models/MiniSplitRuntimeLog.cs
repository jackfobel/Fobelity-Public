using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels.Device.Models
{
  public class MiniSplitRuntimeLog : ITableEntity
  {
    public string PartitionKey { get; set; } // e.g. mini-split ID
    public string RowKey { get; set; } // e.g. log timestamp

    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration => EndTime - StartTime;

    public string Mode { get; set; } // e.g., "cool"
    public string Notes { get; set; }

    public ETag ETag { get; set; } = ETag.All;
    public DateTimeOffset? Timestamp { get; set; }
  }

}
