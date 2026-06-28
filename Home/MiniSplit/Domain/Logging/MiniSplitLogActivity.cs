using Azure.Data.Tables;
using Azure;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DomainModels
{
  public class MiniSplitLogActivity : ITableEntity
  {
    public string PartitionKey { get; set; } = "runtime";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow; // Safe
    public double OutsideTempF { get; set; }
    public double  OutsideHumidity { get; set; }
    public double InsideHumidity { get; set; }
    public int TempSet { get; set; }
    public string Mode { get; set; }
    public bool IsOn { get; set; }
    public bool IsCoolEnabled { get; set; }
    public bool IsHeatEnabled { get; set; }
    public bool FiredRule { get; set; }
    public bool WasOn { get; set; }
    public double InsideTempF { get; set; }
    public string FiredRuleId { get; set; }
    public int? FiredRuleThreshold { get; set; }
    public string? Notes { get; set; }
    public ETag ETag { get; set; } = ETag.All;
    public DateTimeOffset? Timestamp { get; set; }
    public int ThresholdCool { get; set; }
    public int ThresholdHeat { get; set; }

  }

}
