namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class EnergyCostResponse
  {
    public bool Success { get; set; }
    public long Timestamp { get; set; }
    public string TrackingId { get; set; }

    public double TotalCostUSD { get; set; }
    public int SessionCount { get; set; }

    // Optional enhancements
    public double? TotalKWhUsed { get; set; }
    public double? AverageSessionCost { get; set; }
  }
}
