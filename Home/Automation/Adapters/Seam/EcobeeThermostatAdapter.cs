using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;
using Seam.Api;
using Seam.Client;
using Seam.Model;
using HvacModeSettingEnum = Seam.Model.DevicePropertiesCurrentClimateSetting.HvacModeSettingEnum;

namespace Fobelity.Home.Automation.Adapters.Seam
{
  public sealed class EcobeeThermostatAdapter : IDeviceThermostat
  {
    private readonly SeamClient _seam;
    private readonly Thermostats _api;
    private readonly string _deviceId;

    public EcobeeThermostatAdapter(SeamClient seam, Thermostats api, string deviceId)
    {
      _seam = seam;
      _api = api;
      _deviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
    }

    public Task<ThermostatStatus> GetStatusAsync(CancellationToken ct)
    {
      // Grab the thermostat bound to this device
      var t = _seam.Thermostats.List().FirstOrDefault(x => x.DeviceId == _deviceId)
              ?? throw new InvalidOperationException("Ecobee device not found in Seam workspace.");
      var p = t.Properties;

      var modeEnum = p?.CurrentClimateSetting?.HvacModeSetting ?? HvacModeSettingEnum.Unrecognized;
      var mode = modeEnum switch
      {
        HvacModeSettingEnum.Off => "off",
        HvacModeSettingEnum.Heat => "heat",
        HvacModeSettingEnum.Cool => "cool",
        HvacModeSettingEnum.HeatCool => "heat_cool",
        HvacModeSettingEnum.Eco => "eco",
        _ => "unknown"
      };

      // Some installs only expose IsCooling; prefer either flag when present
      var isOn = (p?.IsCooling ?? false) || (p?.IsHeating ?? false);

      var status = new ThermostatStatus(
        Mode: mode,
        SetpointCoolF: p?.CurrentClimateSetting?.CoolingSetPointFahrenheit,
        SetpointHeatF: p?.CurrentClimateSetting?.HeatingSetPointFahrenheit,
        InsideTempF: p?.TemperatureFahrenheit,
        HumidityPct: p?.RelativeHumidity,
        IsOn: isOn,
        LastUpdatedUtc: DateTimeOffset.UtcNow
      );

      return Task.FromResult(status);
    }

    // ---------- helpers ----------
    private static double CToF(double c) => (c * 9.0 / 5.0) + 32.0;

    private (double? coolF, double? heatF) CoerceSetpointsF(ThermostatSetRequest req, string? mode)
    {
      // Prefer the canonical fields if present
      double? coolF = req.SetpointCoolF;
      double? heatF = req.SetpointHeatF;

      // Back-compat fallbacks: TargetTempF/TargetTempC
      if (!coolF.HasValue)
      {
        if (req.TargetTempF.HasValue) coolF = req.TargetTempF.Value;
        else if (req.TargetTempC.HasValue) coolF = Math.Round(CToF(req.TargetTempC.Value));
      }

      if (!heatF.HasValue)
      {
        if (req.TargetTempF.HasValue && string.Equals(mode, "heat", StringComparison.OrdinalIgnoreCase))
          heatF = req.TargetTempF.Value;
        else if (req.TargetTempC.HasValue && string.Equals(mode, "heat", StringComparison.OrdinalIgnoreCase))
          heatF = Math.Round(CToF(req.TargetTempC.Value));
      }

      return (coolF, heatF);
    }

    public async Task<ThermostatStatus> SetAsync(ThermostatSetRequest req, CancellationToken ct)
    {
      // Keep adapter-level dry-run to be resilient if the API layer forgets to short-circuit.
      if (req.DryRun) return await GetStatusAsync(ct);

      var mode = req.Mode?.Trim().ToLowerInvariant();

      // Compute desired setpoints in Fahrenheit, honoring new + legacy fields
      var (coolF, heatF) = CoerceSetpointsF(req, mode);

      // OFF always wins
      if (mode == "off")
      {
        await _api.OffAsync(new Thermostats.OffRequest { DeviceId = _deviceId, Sync = true });
        return await GetStatusAsync(ct);
      }

      // "auto" maps to heat_cool
      if (mode == "auto") mode = "heat_cool";

      // If mode not specified, infer from provided setpoints
      if (string.IsNullOrEmpty(mode))
      {
        if (heatF.HasValue && coolF.HasValue) mode = "heat_cool";
        else if (heatF.HasValue) mode = "heat";
        else if (coolF.HasValue) mode = "cool";
      }

      // Handle ECO preset explicitly if requested
      if (mode == "eco")
      {
        var t = _seam.Thermostats.List().FirstOrDefault(x => x.DeviceId == _deviceId);
        var eco = t?.Properties?.AvailableClimatePresets?.FirstOrDefault(
                    cp => string.Equals(cp.Name, "eco", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(eco?.ClimatePresetKey))
        {
          await _api.ActivateClimatePresetAsync(new Thermostats.ActivateClimatePresetRequest
          {
            DeviceId = _deviceId,
            ClimatePresetKey = eco!.ClimatePresetKey
          });
        }
        return await GetStatusAsync(ct);
      }

      // Primary mode handlers
      switch (mode)
      {
        case "heat_cool":
          // If only one setpoint present, fill the other from current to avoid clearing it.
          if (!heatF.HasValue || !coolF.HasValue)
          {
            var cur = await GetStatusAsync(ct);
            heatF ??= cur.SetpointHeatF;
            coolF ??= cur.SetpointCoolF;
          }

          await _api.HeatCoolAsync(new Thermostats.HeatCoolRequest
          {
            DeviceId = _deviceId,
            HeatingSetPointFahrenheit = (float?)heatF,
            CoolingSetPointFahrenheit = (float?)coolF,
            Sync = true
          });
          return await GetStatusAsync(ct);

        case "cool":
          await _api.CoolAsync(new Thermostats.CoolRequest
          {
            DeviceId = _deviceId,
            CoolingSetPointFahrenheit = (float?)coolF, // may be null → just switches to cool
            Sync = true
          });
          return await GetStatusAsync(ct);

        case "heat":
          await _api.HeatAsync(new Thermostats.HeatRequest
          {
            DeviceId = _deviceId,
            HeatingSetPointFahrenheit = (float?)heatF, // may be null → just switches to heat
            Sync = true
          });
          return await GetStatusAsync(ct);

        case "on":
          // "on" isn’t a native Ecobee mode; choose a sane default:
          // prefer heat_cool if both setpoints available; else pick the one we have;
          // else just return current (no-op).
          if (heatF.HasValue && coolF.HasValue)
          {
            await _api.HeatCoolAsync(new Thermostats.HeatCoolRequest
            {
              DeviceId = _deviceId,
              HeatingSetPointFahrenheit = (float?)heatF,
              CoolingSetPointFahrenheit = (float?)coolF,
              Sync = true
            });
            return await GetStatusAsync(ct);
          }
          if (coolF.HasValue)
          {
            await _api.CoolAsync(new Thermostats.CoolRequest
            {
              DeviceId = _deviceId,
              CoolingSetPointFahrenheit = (float?)coolF,
              Sync = true
            });
            return await GetStatusAsync(ct);
          }
          if (heatF.HasValue)
          {
            await _api.HeatAsync(new Thermostats.HeatRequest
            {
              DeviceId = _deviceId,
              HeatingSetPointFahrenheit = (float?)heatF,
              Sync = true
            });
            return await GetStatusAsync(ct);
          }
          return await GetStatusAsync(ct);

        default:
          // Nothing matched: no-op
          return await GetStatusAsync(ct);
      }
    }
  }
}

