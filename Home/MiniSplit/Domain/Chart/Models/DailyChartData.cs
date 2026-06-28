namespace Fobelity.Home.MiniSplit.Domain.Chart.Models
{
  public class DailyChartData
  {
    public DateTime Date { get; set; }
    public double? AvgOutsideTempF { get; set; }
    public double? TotalCostUSD { get; set; }
    public double? TotalKWhUsed { get; set; }

    public double? ThresholdCool { get; set; }
    public double? MaxOutsideTempF { get; set; }
    public double? MaxInsideTempF { get; set; }
  }



}
