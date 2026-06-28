using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class WeeklySummaryEntry
  {
    public DateTime WeekStartDate { get; set; }
    public double RuntimeHours { get; set; }
    public double TotalKWhUsed { get; set; }
    public double TotalCostUSD { get; set; }
    public double AvgOutsideTempF { get; set; }
    public int SessionCount { get; set; }
  }

}
