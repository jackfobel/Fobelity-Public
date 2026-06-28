using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Device.Models
{
  public sealed class MiniSplitSetRequest
  {
    [JsonPropertyName("dryRun")]
    public bool DryRun { get; init; } = false;

    // Power is optional; if omitted we won’t touch it.
    public bool? Power { get; set; }                 // true|false

    // Mode: "cool","heat","fan","auto","off" (we map to Tuya)
    public string? Mode { get; set; }

    // Preferred: Fahrenheit. If both provided, we prefer °F.
    public int? TargetTempF { get; set; }            // 61..88 (we clamp) → temp_set_f (×10)
    public double? TargetTempC { get; set; }         // 16.0..31.0 (step 0.5) → temp_set (×10)

    // Fan: "auto","low","mid_low","mid","mid_high","high","strong","mute" (+ synonyms)
    public string? FanSpeed { get; set; }

    // Vanes — oscillation (sweep)
    public string? VerticalSweep { get; set; }       // "0","1","2","3"
    public string? HorizontalSweep { get; set; }     // "0","1","2","3","4","5","6","7"

    // Vanes — fixed position (freeze) — overrides sweep if both provided on same axis
    public string? VerticalFreeze { get; set; }      // "0","1","2","3","4","5"
    public string? HorizontalFreeze { get; set; }    // "0","1","2","3","4","5","8","6","7"

    public string? HorizontalDirection { get; set; }  // "left","mid_left","center","mid_right","right","wide","wide_left","wide_right"
    public string? VerticalDirection { get; set; }    // "up","mid_up","center","mid_down","down"
    public int? HorizontalNudge { get; set; }         // + = right, - = left
    public int? VerticalNudge { get; set; }           // + = down,  - = up
  }

}
