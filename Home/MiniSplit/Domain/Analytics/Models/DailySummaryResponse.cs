using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Analytics.Models
{
  public class DailySummaryResponse
  {
    public bool Success { get; set; }
    public long Timestamp { get; set; }
    public string TrackingId { get; set; }
    public List<DailySummaryEntry> DailySummaries { get; set; } = new();
  }
}
