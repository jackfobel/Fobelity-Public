using DomainModels.Storage.Models;

namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class MonthlySummaryResponse
  {
    public bool Success { get; set; }
    public long Timestamp { get; set; }
    public string TrackingId { get; set; }

    public double TotalRuntimeHours { get; set; }
    public int SessionCount { get; set; }
    public double TotalCostUSD { get; set; }
    public double? TotalKWhUsed { get; set; }
    public double? AverageSessionMinutes { get; set; }

    public MiniSplitConfig CoolConfig { get; set; }
    public MiniSplitConfig HeatConfig { get; set; }
  }
}
