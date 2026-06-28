using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class HourlySummaryEntry
  {
    public DateTime Hour { get; set; }
    public double RuntimeMinutes { get; set; }
    public double AvgOutsideTempF { get; set; }
    public double EnergyCostUSD { get; set; }
    public double EnergyKWhUsed { get; set; }
    public double ThresholdCool { get; set; }
    public int TempSet { get; set; }
    public double OutsideTempF { get; set; }
    public double InsideTempF { get; set; }
    public bool IsOn { get; set; }
  }
}
