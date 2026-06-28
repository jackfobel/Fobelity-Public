namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class MiniSplitRuntimeAnalyticsResponse
  {
    public bool Success { get; set; }
    public long Timestamp { get; set; }
    public string TrackingId { get; set; } = string.Empty;

    public double TotalRuntimeHours { get; set; }
    public double? AverageSessionMinutes { get; set; }
    public int SessionCount { get; set; }
  }

}
