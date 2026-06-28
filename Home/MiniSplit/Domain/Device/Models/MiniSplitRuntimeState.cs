using Azure;
using Azure.Data.Tables;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels.Device.Models
{
  public class MiniSplitRuntimeState : ITableEntity
  {
    public string PartitionKey { get; set; } = "runtimestate";
    public string RowKey { get; set; } = "CurrentState";

    public bool IsOn { get; set; }
    public DateTimeOffset LastChanged { get; set; }

    // Required for ITableEntity
    public ETag ETag { get; set; } = ETag.All;
    public DateTimeOffset? Timestamp { get; set; }
  }
}
