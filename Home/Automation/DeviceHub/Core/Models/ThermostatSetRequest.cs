using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.DeviceHub.Core.Models
{
  /// <summary>
  /// Canonical cross-provider thermostat set request. Optional fields are ignored when null.
  /// </summary>
  public sealed record ThermostatSetRequest
  {
    public string? Mode { get; init; }

    // Setpoints (existing)
    public int? TargetTempF { get; init; }
    public double? TargetTempC { get; init; }
    public int? SetpointCoolF { get; init; }
    public int? SetpointHeatF { get; init; }

    public string? FanSpeed { get; init; }

    // Oscillation (existing)
    public string? VerticalSwing { get; init; }     // "0".."3"
    public string? HorizontalSwing { get; init; }   // "0".."7"

    // Freeze (existing)
    public string? VerticalFreeze { get; init; }    // "0".."5"
    public string? HorizontalFreeze { get; init; }  // "0","1","2","3","4","5","8","6","7"

    // NEW: human-friendly absolute directions
    // Horizontal: "left","mid_left","center","mid_right","right","wide","wide_left","wide_right"
    public string? HorizontalDirection { get; init; }
    // Vertical:   "up","mid_up","center","mid_down","down"
    public string? VerticalDirection { get; init; }

    // NEW: relative nudges (steps). Horizontal: + = right, Vertical: + = down
    public int? HorizontalNudge { get; init; }    // e.g., +1 = a little more right; -1 = a little more left
    public int? VerticalNudge { get; init; }      // e.g., +1 = a little more down; -1 = a little more up

    public bool DryRun { get; init; } = false;
  }
}
