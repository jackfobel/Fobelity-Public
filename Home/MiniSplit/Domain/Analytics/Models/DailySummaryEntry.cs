using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class DailySummaryEntry
  {
    public DateTime Date { get; set; }
    public double RuntimeHours { get; set; }
    public double TotalKWhUsed { get; set; }
    public double TotalCostUSD { get; set; }
    public int SessionCount { get; set; }
    public double? AvgOutsideTempF { get; set; }

    // NEW fields for charts
    public double? MaxOutsideTempF { get; set; }
    public double? MaxInsideTempF { get; set; }
    public int? ThresholdCool { get; set; }

    public DateTime? TurnedOn { get; set; }
    public DateTime? TurnedOff { get; set; }
  }


}
