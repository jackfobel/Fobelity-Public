using DomainModels.Command.Interfaces;
using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Device.Models;
using Fobelity.Home.MiniSplit.Domain.Device.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;

namespace BackendServices
{
  public static class DeviceCapabilitiesHelper
  {
    // Behavior notes:
    //Absolute directions(e.g., "right", "up") always win if present.
    //Nudges fall back to center if we can’t read a current freeze position or if you’re in a “wide” code(6/7/8).
    //Any direction/nudge sets a freeze DP so the louver holds the new position(it won’t keep sweeping).

    public static int ToRaw(double value, int scale) => (int)Math.Round(value * Math.Pow(10, scale));

    // Mode mapping to Tuya
    public static string MapMode(string? m) => (m ?? "").Trim().ToLowerInvariant() switch
    {
      "cool" => "cold",
      "heat" => "hot",
      "dry" => "wet",
      "fan" => "wind",
      "auto" => "auto",
      "cold" or "hot" or "wet" or "wind" => m!.ToLowerInvariant(),
      "off" => "off",
      _ => ""
    };

    // Fan mapping to `windspeed`
    public static string MapFan(string? f) => (f ?? "").Trim().ToLowerInvariant() switch
    {
      "" => "",
      "auto" => "auto",
      "quiet" or "silent" or "mute" => "mute",
      "low" => "low",
      "low-med" or "low_medium" or "medium-low" or "mid-low" => "mid_low",
      "med" or "medium" or "mid" => "mid",
      "med-high" or "mid-high" => "mid_high",
      "high" => "high",
      "turbo" or "max" or "strong" => "strong",
      _ => "auto"
    };

    public static readonly HashSet<string> UpDownSweepVals = new(["0", "1", "2", "3"]);
    public static readonly HashSet<string> LeftRightSweepVals = new(["0", "1", "2", "3", "4", "5", "6", "7"]);
    public static readonly HashSet<string> UpDownFreezeVals = new(["0", "1", "2", "3", "4", "5"]);
    public static readonly HashSet<string> LeftRightFreezeVals = new(["0", "1", "2", "3", "4", "5", "6", "7", "8"]);

    // Simple membership guard (the bit you were missing)
    public static bool IsAllowed(string value, ISet<string> allowed) =>
        !string.IsNullOrWhiteSpace(value) && allowed.Contains(value);

    public static void AddEnumIfValid(List<DomainModels.Command.Models.TuyaCommandPayload.Command> cmds,
                               string code, string? val, HashSet<string> allowed)
    {
      if (!string.IsNullOrWhiteSpace(val) && allowed.Contains(val))
        cmds.Add(new() { code = code, value = val });
    }

    // Nudge along a 1D ordered track; clamps at ends
    public static string NudgeLinear(string seed, int delta, string[] track, string defaultCode = "3")
    {
      var idx = Array.IndexOf(track, seed);
      if (idx < 0) idx = Array.IndexOf(track, defaultCode);
      idx = Math.Clamp(idx + delta, 0, track.Length - 1);
      return track[idx];
    }




    public static readonly Dictionary<string, string> HorizDirMap = new(StringComparer.OrdinalIgnoreCase)
    {
      ["left"] = "1",
      ["mid_left"] = "2",
      ["midleft"] = "2",
      ["slightly_left"] = "2",
      ["center"] = "3",
      ["middle"] = "3",
      ["mid_right"] = "4",
      ["midright"] = "4",
      ["slightly_right"] = "4",
      ["right"] = "5",
      ["wide"] = "8",
      ["wide_left"] = "6",
      ["wide_right"] = "7",
    };

    public static readonly Dictionary<string, string> VertDirMap = new(StringComparer.OrdinalIgnoreCase)
    {
      ["up"] = "1",
      ["top"] = "1",
      ["mid_up"] = "2",
      ["slightly_up"] = "2",
      ["center"] = "3",
      ["middle"] = "3",
      ["mid_down"] = "4",
      ["slightly_down"] = "4",
      ["down"] = "5",
      ["bottom"] = "5",
    };



  }

}

