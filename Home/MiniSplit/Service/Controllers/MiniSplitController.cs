using BackendServices;
using ComfortRulesEngine.Base;
using DomainModels.Command.Interfaces;
using DomainModels.Command.Models;
using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Device.Models;
using DomainModels.Email.Interfaces;
using DomainModels.RulesEngine;
using DomainModels.Storage.Interfaces;
using DomainModels.Storage.Models;
using DomainModels.Weather;
using Fobelity.Home.MiniSplit.Domain.Device.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NRules;
using NRules.Diagnostics;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Utilities;
using System.Runtime.Intrinsics.Arm;
using System.Security.Claims;

namespace MiniSplitControlService.Controllers
{
  //[Authorize(Roles = "MiniSplit.Controller,MiniSplit.Scheduler,MiniSplit.UserAccess,API.Invoker")]
  [ApiController]
  [Authorize]
  [Route("api/[controller]")]
  public class MiniSplitController : ControllerBase
  {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MiniSplitController> _logger;
    private readonly ICommandService _commandService;
    private readonly IDeviceService _deviceService;
    private readonly IConfigurationService _configurationService;
    private readonly ITableStorageService _tableStorageService;
    private readonly ISessionFactory _sessionFactory;
    private readonly IEmailService _emailService;
    private readonly IWeatherService _weatherService;

    // Configuration Data from Azure Table Storage.
    private MiniSplitConfig? MiniSplitConfigCool { get; set; }
    private MiniSplitConfig? MiniSplitConfigHeat { get; set; }

    // Inject services via constructor
    public MiniSplitController(
      IHttpClientFactory httpClientFactory,
      ILogger<MiniSplitController> logger,
      ICommandService commandService,
      IDeviceService deviceService,
      IConfigurationService configurationService,
      ITableStorageService tableStorageService,
      ISessionFactory rulesSessionFactory,
      IEmailService emailService,
      IWeatherService weatherService)
    {
      _httpClientFactory = httpClientFactory;
      _logger = logger;
      _commandService = commandService;
      _deviceService = deviceService;
      _configurationService = configurationService;
      _tableStorageService = tableStorageService;
      _sessionFactory = rulesSessionFactory;
      _emailService = emailService;
      _weatherService = weatherService;
    }

    // TODO: Move these to DeviceCapabilitiesHelper or similar
    // Allowed values (existing)
    static readonly HashSet<string> UpDownSweepVals = new(["0", "1", "2", "3"]);
    static readonly HashSet<string> LeftRightSweepVals = new(["0", "1", "2", "3", "4", "5", "6", "7"]);
    static readonly HashSet<string> UpDownFreezeVals = new(["0", "1", "2", "3", "4", "5"]);
    static readonly HashSet<string> LeftRightFreezeVals = new(["0", "1", "2", "3", "4", "5", "6", "7", "8"]);

    // Linear order for nudging (ignore "wide" variants)
    static readonly string[] HLinear = new[] { "1", "2", "3", "4", "5" }; // left -> right
    static readonly string[] VLinear = new[] { "1", "2", "3", "4", "5" }; // up   -> down
                                                                          // Absolute direction maps
                                                                          // Small carrier for current vane positions
    private sealed record VaneState(string? HorzFreeze, string? VertFreeze, string? HorzSweep, string? VertSweep)
    {
      // Lets you write: var (curLR, curUD) = await GetCurrentVanesAsync();
      public void Deconstruct(out string? leftRightFreeze, out string? upDownFreeze)
      {
        leftRightFreeze = HorzFreeze;
        upDownFreeze = VertFreeze;
      }

      // (Optional) full 4-out if you ever want it:
      public void Deconstruct(out string? horzFreeze, out string? vertFreeze, out string? horzSweep, out string? vertSweep)
      {
        horzFreeze = HorzFreeze;
        vertFreeze = VertFreeze;
        horzSweep = HorzSweep;
        vertSweep = VertSweep;
      }
    }

    private static readonly string[] HFreezeTrack = { "1", "2", "3", "4", "5" }; // left..right
    private static readonly string[] VFreezeTrack = { "1", "2", "3", "4", "5" }; // up..down

    private static string MapHorizontalDirectionToFreeze(string friendly) => friendly.ToLowerInvariant() switch
    {
      "left" => "1",
      "mid_left" => "2",
      "center" => "3",
      "mid_right" => "4",
      "right" => "5",
      "wide" => "8",
      "wide_left" => "6",
      "wide_right" => "7",
      _ => "3" // center default
    };

    private static string MapVerticalDirectionToFreeze(string friendly) => friendly.ToLowerInvariant() switch
    {
      "up" => "1",
      "mid_up" => "2",
      "center" => "3",
      "mid_down" => "4",
      "down" => "5",
      _ => "3"
    };

    private static string Nudge(string[] track, string? currentCode, int delta, string defaultCode)
    {
      // Seed: if Tuya reports "0" (current position sentinel) or null, assume default
      var seed = string.IsNullOrEmpty(currentCode) || currentCode == "0" ? defaultCode : currentCode;

      var idx = Array.IndexOf(track, seed);
      if (idx < 0) idx = Array.IndexOf(track, defaultCode); // safe fallback
      idx = Math.Clamp(idx + delta, 0, track.Length - 1);
      return track[idx];
    }


    // Fetch current vane freeze/sweep positions (robust against shape changes)
    private async Task<VaneState> GetCurrentVanesAsync()
    {
      var statusResult = await GetStatus();
      if (statusResult is not OkObjectResult ok || ok.Value is null)
        return new VaneState(null, null, null, null);

      // We don't depend on a specific DeviceStatus type;
      // read via JToken so camelCase/PascalCase/snake_case all work.
      var j = JToken.FromObject(ok.Value);

      // Try multiple casings for each field:
      string? GetString(string a, string b, string c) =>
          j.SelectToken(a)?.Value<string>()
       ?? j.SelectToken(b)?.Value<string>()
       ?? j.SelectToken(c)?.Value<string>();

      var lrFreeze = GetString("LeftRightFreeze", "leftRightFreeze", "left_right_freeze");
      var udFreeze = GetString("UpDownFreeze", "upDownFreeze", "up_down_freeze");
      var lrSweep = GetString("LeftRightSweep", "leftRightSweep", "left_right_sweep");
      var udSweep = GetString("UpDownSweep", "upDownSweep", "up_down_sweep");

      return new VaneState(lrFreeze, udFreeze, lrSweep, udSweep);
    }

    // Behavior notes:
    //Absolute directions(e.g., "right", "up") always win if present.
    //Nudges fall back to center if we can’t read a current freeze position or if you’re in a “wide” code(6/7/8).
    //Any direction/nudge sets a freeze DP so the louver holds the new position(it won’t keep sweeping).
    public async Task<List<DomainModels.Command.Models.TuyaCommandPayload.Command>>
      BuildCommandsAsync(MiniSplitSetRequest req)
    {
      var cmds = new List<DomainModels.Command.Models.TuyaCommandPayload.Command>();

      // ---- Power ----
      if (req.Power is bool p)
        cmds.Add(new() { code = "switch", value = p });

      // ---- Mode ----
      var mappedMode = DeviceCapabilitiesHelper.MapMode(req.Mode);
      if (mappedMode == "off")
      {
        cmds.Add(new() { code = "switch", value = false });
      }
      else if (!string.IsNullOrWhiteSpace(mappedMode))
      {
        cmds.Add(new() { code = "switch", value = true });
        cmds.Add(new() { code = "mode", value = mappedMode });
      }

      // ---- Temperature (prefer F) ----
      if (req.TargetTempF is int fDeg)
      {
        var clamped = Math.Clamp(fDeg, 61, 88);
        cmds.Add(new() { code = "temp_unit_convert", value = "f" });
        cmds.Add(new() { code = "temp_set_f", value = DeviceCapabilitiesHelper.ToRaw(clamped, 1) });
      }
      else if (req.TargetTempC is double cDeg)
      {
        var clamped = Math.Clamp(cDeg, 16.0, 31.0);
        var snapped = Math.Round(clamped / 0.5) * 0.5;
        cmds.Add(new() { code = "temp_unit_convert", value = "c" });
        cmds.Add(new() { code = "temp_set", value = DeviceCapabilitiesHelper.ToRaw(snapped, 1) });
      }

      // ---- Fan ----
      var fan = DeviceCapabilitiesHelper.MapFan(req.FanSpeed);
      if (!string.IsNullOrWhiteSpace(fan))
        cmds.Add(new() { code = "windspeed", value = fan });

      // ---- Vanes: decide per-axis once (Direction > Freeze > Nudge > Sweep) ----
      string? finalHFreeze = null, finalVFreeze = null;
      string? finalHSweep = null, finalVSweep = null;

      // Horizontal: Direction
      if (!string.IsNullOrWhiteSpace(req.HorizontalDirection) &&
          DeviceCapabilitiesHelper.HorizDirMap.TryGetValue(req.HorizontalDirection!, out var hAbs))
      {
        finalHFreeze = hAbs;
      }
      // Horizontal: explicit Freeze
      else if (!string.IsNullOrWhiteSpace(req.HorizontalFreeze) &&
               DeviceCapabilitiesHelper.IsAllowed(req.HorizontalFreeze!, DeviceCapabilitiesHelper.LeftRightFreezeVals))
      {
        finalHFreeze = req.HorizontalFreeze!;
      }
      // Horizontal: Nudge (needs current)
      else if ((req.HorizontalNudge ?? 0) != 0)
      {
        var (curLR, curUD) = await GetCurrentVanesAsync(); // tuple: (left_right_freeze, up_down_freeze)

        // If in "wide"/unknown, start from center "3"
        var baseCode = HLinear.Contains(curLR ?? "") ? curLR! : "3";
        var target = DeviceCapabilitiesHelper.NudgeLinear(baseCode, req.HorizontalNudge!.Value, HLinear);
        finalHFreeze = target;
      }
      // Horizontal: Sweep (only if nothing else)
      else if (!string.IsNullOrWhiteSpace(req.HorizontalSweep) &&
               DeviceCapabilitiesHelper.IsAllowed(req.HorizontalSweep!, DeviceCapabilitiesHelper.LeftRightSweepVals))
      {
        finalHSweep = req.HorizontalSweep!;
      }

      // Vertical: Direction
      if (!string.IsNullOrWhiteSpace(req.VerticalDirection) &&
          DeviceCapabilitiesHelper.VertDirMap.TryGetValue(req.VerticalDirection!, out var vAbs))
      {
        finalVFreeze = vAbs;
      }
      // Vertical: explicit Freeze
      else if (!string.IsNullOrWhiteSpace(req.VerticalFreeze) &&
               DeviceCapabilitiesHelper.IsAllowed(req.VerticalFreeze!, DeviceCapabilitiesHelper.UpDownFreezeVals))
      {
        finalVFreeze = req.VerticalFreeze!;
      }
      // Vertical: Nudge (needs current)
      else if ((req.VerticalNudge ?? 0) != 0)
      {
        var (curLR, curUD) = await GetCurrentVanesAsync();

        var baseCode = VLinear.Contains(curUD ?? "") ? curUD! : "3";
        var target = DeviceCapabilitiesHelper.NudgeLinear(baseCode, req.VerticalNudge!.Value, VLinear);
        finalVFreeze = target;
      }
      // Vertical: Sweep
      else if (!string.IsNullOrWhiteSpace(req.VerticalSweep) &&
               DeviceCapabilitiesHelper.IsAllowed(req.VerticalSweep!, DeviceCapabilitiesHelper.UpDownSweepVals))
      {
        finalVSweep = req.VerticalSweep!;
      }

      // Emit per-axis result (freeze wins over sweep)
      if (!string.IsNullOrWhiteSpace(finalHFreeze))
        cmds.Add(new() { code = "left_right_freeze", value = finalHFreeze! });
      else if (!string.IsNullOrWhiteSpace(finalHSweep))
        cmds.Add(new() { code = "left_right_sweep", value = finalHSweep! });

      if (!string.IsNullOrWhiteSpace(finalVFreeze))
        cmds.Add(new() { code = "up_down_freeze", value = finalVFreeze! });
      else if (!string.IsNullOrWhiteSpace(finalVSweep))
        cmds.Add(new() { code = "up_down_sweep", value = finalVSweep! });

      return cmds;
    }



    [HttpPost("set")]
    [ProducesResponseType(typeof(DeviceStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Set([FromBody] MiniSplitSetRequest req)
    {
      if (req.DryRun)
      {
        // No Tuya calls — just echo current status
        return await GetStatus();
      }

      var commands = await BuildCommandsAsync(req);
      var payload = new TuyaCommandPayload { commands = commands.ToArray() };

      var res = await _commandService.SendCommandPost<DeviceAccessResponse>(
                  "SendDeviceAction",
                  _configurationService.IoTDeviceId,
                  _deviceService,
                  payload);

      if (!res.IsSuccessStatusCode)
        return StatusCode(500, new { message = "Failed to send commands to device." });

      await Task.Delay(1000); // brief settle
      return await GetStatus();
    }




    // TODO: Move somewhere else....
    private async Task<IActionResult> SendAndReturnStatusAsync(
      List<DomainModels.Command.Models.TuyaCommandPayload.Command> cmds)
    {
      var jsonPayload = new DomainModels.Command.Models.TuyaCommandPayload
      {
        commands = cmds.ToArray()
      };

      var resp = await _commandService.SendCommandPost<DeviceAccessResponse>(
          "SendDeviceAction",
          _configurationService.IoTDeviceId,
          _deviceService,
          jsonPayload
      );

      if (!resp.IsSuccessStatusCode)
        return StatusCode(500, new { message = "Failed to apply command(s) to mini-split." });

      // Match your turn-on/off pattern
      await Task.Delay(3000);

      var statusResult = await GetStatus();
      if (statusResult is OkObjectResult ok && ok.Value is DeviceStatus deviceStatus)
      {
        // optional: SignalR broadcast here if you want
        // await _hubContext.Clients.All.SendAsync("MiniSplitReceiveStatusMessage", deviceStatus);
        return Ok(deviceStatus);
      }

      return StatusCode(500, new { message = "Command OK but failed to read back status." });
    }

    [HttpPost("automate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Automate([FromBody] WeatherModel weatherModel)
    {
      _logger.LogInformation("Entering into Automate...");

      //_logger.LogInformation($"User: {User.Identity?.Name}");
      //foreach (var claim in User.Claims)
      //{
      //  _logger.LogInformation($"Claim: {claim.Type} = {claim.Value}");
      //}

      //_logger.LogInformation("Roles: {Roles}", string.Join(", ",
      //  User.Claims
      //      .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
      //      .Select(c => c.Value)));


      // Fetch configuration from Azure Table Storage
      MiniSplitConfigCool = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "cold");
      MiniSplitConfigHeat = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "heat");

      // Get the current status of the mini-split
      var statusResult = await GetStatus();

      var okResult = statusResult as OkObjectResult;
      var deviceStatus = okResult?.Value as DeviceStatus;

      // SignalR: Notify all clients of the status update.
      //await _hubContext.Clients.All.SendAsync("MiniSplitReceiveStatusMessage", deviceStatus);

      var jsonDeviceStatus = JsonConvert.SerializeObject(deviceStatus);
      _logger.LogInformation($"ℹ️ automate - Status: {jsonDeviceStatus}");

      //var centralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
      //var centralTime = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, centralTimeZone);
      DateTimeOffset utcTime = DateTimeOffset.UtcNow;

      var runtimeLog = new MiniSplitLogActivity
      {
        PartitionKey = "minisplit",
        RowKey = Guid.NewGuid().ToString(),
        //StartTimeUtc = DateTime.UtcNow,
        OutsideTempF = (int)Math.Round(weatherModel.Main.Temp),
        OutsideHumidity = weatherModel.Main.Humidity,
        InsideHumidity = deviceStatus.HumidityCurrent,
        InsideTempF = deviceStatus.TempCurrentF,
        Mode = deviceStatus.Mode,
        WasOn = deviceStatus.Switch,
        IsOn = deviceStatus.Switch,
        IsCoolEnabled = MiniSplitConfigCool.enabled,
        IsHeatEnabled = MiniSplitConfigHeat.enabled,
        Timestamp = utcTime,
        TempSet = deviceStatus.TempSetF,
        ThresholdCool = MiniSplitConfigCool.threshhold,
        ThresholdHeat = MiniSplitConfigHeat.threshhold,
        Notes = "Running..."
      };

      // Save early so it's ready to be updated later - Will continue to update this log entry.
      await _tableStorageService.AddEntityAsync("minisplitlogs", runtimeLog);



      double roundedTemp = Math.Round(weatherModel.Main.Temp, 0);
      _logger.LogInformation($"Outside Temperature: {roundedTemp}");
      _logger.LogInformation($"Outside Humidity: {weatherModel.Main.Humidity}");

      // Insert facts & fire rules
      var weatherData = new CurrentWeatherData
      {
        Timestamp = DateTime.Now,
        Temperature = roundedTemp,
        Humidity = weatherModel.Main.Humidity
      };

      var coolTempRule = new TemperatureRule
      {
        Id = MiniSplitConfigCool.RowKey,
        Enabled = MiniSplitConfigCool.enabled,
        Threshhold = MiniSplitConfigCool.threshhold
      };

      var heatTempRule = new TemperatureRule
      {
        Id = MiniSplitConfigHeat.RowKey,
        Enabled = MiniSplitConfigHeat.enabled,
        Threshhold = MiniSplitConfigHeat.threshhold
      };

      _logger.LogInformation($"Running rules on Current Temperature: {roundedTemp}, Humidity: {weatherData.Humidity}, Mini-split Status: {deviceStatus.Switch}, Mini-split Mode: {deviceStatus.Mode}");


      // Create session & execute rules
      var session = _sessionFactory.CreateSession();


      session.Insert(weatherData);
      session.Insert(deviceStatus);
      session.Insert(runtimeLog);



      if (MiniSplitConfigCool.enabled)
      {
        //var cooldownWindow = TimeSpan.FromMinutes(15);
        //var tableName = "minisplitlogs";

        //var shouldFireCoolOn = !(await _tableStorageService.WasRecentlyToggledAsync(tableName, "cold", cooldownWindow));
        //var shouldFireCoolOff = !(await _tableStorageService.WasRecentlyToggledAsync(tableName, "cold", cooldownWindow));

        //session.Insert(new RuleCooldownStatus("CoolOn", shouldFireCoolOn));
        //session.Insert(new RuleCooldownStatus("CoolOff", shouldFireCoolOff));



        session.Insert(coolTempRule);
      }

      if (MiniSplitConfigHeat.enabled)
      {
        session.Insert(heatTempRule);
      }



      // DEBUG
      _logger.LogInformation("EVAL: weather={Temp}, threshold={Thresh}, pass={Pass}",
      weatherData.Temperature, coolTempRule.Threshhold, weatherData.Temperature < coolTempRule.Threshhold);

      _logger.LogInformation("EVAL: Switch={Switch}, Mode={Mode}, pass={Pass}",
      deviceStatus.Switch, deviceStatus.Mode, deviceStatus.Switch && deviceStatus.Mode == "cold");


      session.Events.RuleFiringEvent += (sender, args) =>
        OnRuleFiringEvent(sender, args, _logger, _tableStorageService, runtimeLog);


      _logger.LogInformation($"Heat Threshold: {heatTempRule.Threshhold}, Cool Threshold: {coolTempRule.Threshhold}");
      _logger.LogInformation($"Inserted TemperatureRule: {coolTempRule.Id} (cool), {heatTempRule.Id} (heat)");


      // DEBUG
      var factSummaries = session.Query<object>().GroupBy(f => f.GetType().Name)
          .Select(g => $"{g.Key} x {g.Count()}");
      _logger.LogInformation($"Facts in session before Fire(): {string.Join(", ", factSummaries)}");

      // DEBUG
      _logger.LogInformation("LOGGING:Dumping all facts before firing:");
      foreach (var fact in session.Query<object>())
      {
        _logger.LogInformation("LOGGING:Fact: {Type} => {@Fact}", fact.GetType().Name, fact);
      }

      // DEBUG
      var allTempRules = session.Query<TemperatureRule>().ToList();
      foreach (var rule in allTempRules)
      {
        _logger.LogInformation("TempRule in session: Id={Id}, Enabled={Enabled}, Threshold={Threshold}",
          rule.Id, rule.Enabled, rule.Threshhold);
      }




      // DEBUG
      var tracker = new RuleActivationTracker();
      session.Events.ActivationCreatedEvent += tracker.OnActivationCreated;
      session.Events.RuleFiredEvent += tracker.OnRuleFired;


      int rulesFired = session.Fire();


      // DEBUG
      foreach (var (rule, facts) in tracker.GetActivatedFacts())
      {
        _logger.LogInformation("Rule activated: {Rule}", rule.Name);
        foreach (var fact in facts)
        {
          _logger.LogInformation("  -> Fact: {Type} = {@Fact}", fact.GetType().Name, fact);
        }
      }
      foreach (var rule in tracker.GetFiredRules())
      {
        _logger.LogInformation("Rule fired: {Rule}", rule.Name);
      }

      // DEBUG
      if (rulesFired == 0)
      {
        _logger.LogInformation("No rules were fired during this session.");
        runtimeLog.Notes = "No Rules Fired.";
        await _tableStorageService.UpdateEntityAsync("minisplitlogs", runtimeLog);
      }
      //else
      //{
      //  _logger.LogInformation($"{rulesFired} rule(s) fired during this session.");
      //  //runtimeLog.Notes = $"{rulesFired} Rules Fired.";
      //  await _tableStorageService.UpdateEntityAsync("minisplitlogs", runtimeLog);
      //}




      //// 
      //runtimeLog.OutsideTempF = weatherData.Temperature;
      //runtimeLog.OutsideHumidity = weatherData.Humidity;
      //runtimeLog.Mode = deviceStatus.Mode ?? "unknown";
      //runtimeLog.IsOn = deviceStatus.Switch;
      //runtimeLog.InsideTempF = deviceStatus.TempCurrentF;


      //await _tableStorageService.UpdateEntityAsync("minisplitlogs", runtimeLog);
      _logger.LogInformation("Logged runtime stats to MiniSplitLogs table from Automate.");


      //return Ok("MiniSplitController: Automation executed successfully.");
      return NoContent();
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(DeviceStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetStatus()
    {
      _logger.LogInformation($"MiniSplitController: GetStatus called.");

      // Debugging information. 
      _logger.LogInformation("User: {User}", User.Identity?.Name ?? "Anonymous");
      _logger.LogInformation("Roles: {Roles}", string.Join(", ",
          User.Claims
              .Where(c => c.Type == ClaimTypes.Role || c.Type == "roles")
              .Select(c => c.Value)));

      var iotDeviceStatus = await _commandService.SendCommand<IoTDeviceStatus>(
          "GetDeviceStatus",
          _configurationService.IoTDeviceId,
          _deviceService
      );

      if (!iotDeviceStatus.success)
        return StatusCode(500, new { message = "Failed to retrieve mini-split status." });

      // Convert our IoT Device Status to a more friendly model.
      string iotDeviceStatusJson = JsonConvert.SerializeObject(iotDeviceStatus);
      var deviceStatus = DeviceStatusParser.Parse(iotDeviceStatusJson);

      // SignalR: Notify all clients of the status update.
      //await _hubContext.Clients.All.SendAsync("MiniSplitReceiveStatusMessage", deviceStatus);

      return Ok(deviceStatus);
    }

    [HttpGet("device-details")]
    [ProducesResponseType(typeof(DeviceDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDeviceDetails()
    {
      var deviceDetails = await _commandService.SendCommand<DeviceDetails>(
          "GetDeviceDetails",
          _configurationService.IoTDeviceId,
          _deviceService
      );

      if (!deviceDetails.success)
        return StatusCode(500, new { message = "Failed to retrieve device status." });

      return Ok(deviceDetails);
    }

    [HttpPost("turn-on")]
    [ProducesResponseType(typeof(DeviceStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TurnOnDevice()
    {
      var jsonPayload = new TuyaCommandPayload
      {
        commands = new[]
        {
          new TuyaCommandPayload.Command { code = "switch", value = true }
        }
      };

      var iotDeviceStatus = await _commandService.SendCommandPost<DeviceAccessResponse>(
          "SendDeviceAction",
          _configurationService.IoTDeviceId,
          _deviceService,
          jsonPayload
      );

      if (!iotDeviceStatus.IsSuccessStatusCode)
        return StatusCode(500, new { message = "Failed to retrieve mini-split status." });

      // Let's wait 3 seconds before attempting to get the latest status.
      await Task.Delay(3000);

      // Get status after turning on.
      var statusResult = await GetStatus();
      var okResult = statusResult as OkObjectResult;
      var deviceStatus = okResult?.Value as DeviceStatus;

      // SignalR: Notify all clients of the status update.
      //await _hubContext.Clients.All.SendAsync("MiniSplitReceiveStatusMessage", deviceStatus);

      var jsonDeviceStatus = JsonConvert.SerializeObject(deviceStatus);
      _logger.LogInformation($"ℹ️ TurnOnDevice - Status: {jsonDeviceStatus}");

      return Ok(deviceStatus);
    }

    [HttpPost("turn-off")]
    [ProducesResponseType(typeof(DeviceStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> TurnOffDevice()
    {
      var jsonPayload = new TuyaCommandPayload
      {
        commands = new[]
        {
          new TuyaCommandPayload.Command { code = "switch", value = false }
        }
      };

      var iotDeviceStatus = await _commandService.SendCommandPost<DeviceAccessResponse>(
          "SendDeviceAction",
          _configurationService.IoTDeviceId,
          _deviceService,
          jsonPayload
      );

      if (!iotDeviceStatus.IsSuccessStatusCode)
        return StatusCode(500, new { message = "Failed to retrieve mini-split status." });

      // Let's wait 3 seconds before attempting to get the latest status.
      await Task.Delay(3000);

      // Get status after turning off.
      var statusResult = await GetStatus();
      var okResult = statusResult as OkObjectResult;
      var deviceStatus = okResult?.Value as DeviceStatus;

      // SignalR: Notify all clients of the status update.
      // await _hubContext.Clients.All.SendAsync("MiniSplitReceiveStatusMessage", deviceStatus);

      var jsonDeviceStatus = JsonConvert.SerializeObject(deviceStatus);
      _logger.LogInformation($"ℹ️ TurnOffDevice - Status: {jsonDeviceStatus}");

      return Ok(deviceStatus);
    }

    [HttpGet("config-data")]
    [ProducesResponseType(typeof(MiniSplitConfigDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> LoadConfigData()
    {
      var miniSplitConfigCool = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "cold");
      var miniSplitConfigHeat = await _tableStorageService.GetEntityAsync<MiniSplitConfig>("minisplitconfig", "config", "heat");

      if (miniSplitConfigCool == null || miniSplitConfigHeat == null)
        return StatusCode(500, new { message = "Failed to retrieve config data." });

      var configData = new MiniSplitConfigDataResponse()
      {
        result = new MiniSplitConfigData()
        {
          MiniSplitConfigCool = miniSplitConfigCool,
          MiniSplitConfigHeat = miniSplitConfigHeat
        },
        success = true,
        t = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        tid = Guid.NewGuid().ToString()
      };

      return Ok(configData);
    }

    [HttpPost("update-config")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateConfig([FromBody] MiniSplitConfigData configData)
    {
      if (configData == null || configData.MiniSplitConfigCool == null || configData.MiniSplitConfigHeat == null)
        return BadRequest(new { message = "Invalid config data." });

      try
      {
        await _tableStorageService.UpdateEntityAsync("minisplitconfig", configData.MiniSplitConfigCool);
        await _tableStorageService.UpdateEntityAsync("minisplitconfig", configData.MiniSplitConfigHeat);

        return NoContent();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to update mini-split config.");
        return StatusCode(500, new { message = "Failed to update config." });
      }
    }

    [HttpGet("current-weather")]
    [ProducesResponseType(typeof(WeatherModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetCurrentTemperature()
    {
      try
      {
        var weather = await _weatherService.GetCurrentTemperature();
        return Ok(weather);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Failed to fetch current temperature.");
        return StatusCode(500, new { message = "Failed to fetch current temperature." });
      }
    }



    public static async Task OnRuleFiringEvent(
      object sender,
      AgendaEventArgs eventArgs,
      ILogger<MiniSplitController> logger,
      ITableStorageService tableStorageService,
      MiniSplitLogActivity miniSplitRuntimeLog)
    {
      logger.LogInformation($"MiniSplitController: OnRuleFiringEvent: Rule about to fire: {eventArgs.Rule.Name}");
      var session = (NRules.ISession)sender;
      var facts = session.Query<CurrentWeatherData>().ToList();

      // Query for Facts (Current Weather passed in).
      foreach (var fact in facts)
      {
        logger.LogInformation($"MiniSplitController: OnRuleFiringEvent: WeatherData - Temperature: {fact.Temperature}, Humidity: {fact.Humidity}");
      }

      // Query for TemperatureRule facts
      var temperatureRuleFacts = session.Query<TemperatureRule>().ToList();
      foreach (var rule in temperatureRuleFacts)
      {
        logger.LogInformation($"MiniSplitController: OnRuleFiringEvent: TemperatureRule - Id: {rule.Id}, Enabled: {rule.Enabled}, Threshhold: {rule.Threshhold}");
      }

      // DEBUG
      var ConditionsMet = eventArgs.Rule.LeftHandSide.ChildElements
        .SelectMany(element => element.Exports)
        .Where(export => export.Target is NRules.RuleModel.PatternElement)
        .SelectMany(export => ((NRules.RuleModel.PatternElement)export.Target).Expressions)
        .ToList();

      var Expressions = ConditionsMet.Select(a => a.Expression.Body).ToList();
      foreach (var expr in Expressions)
      {
        //allConditions.Add(ExpressionHelper.AccessDebugView(expr));
        logger.LogInformation($"MiniSplitController: OnRuleFiringEvent: Condition Met: {expr}");
      }


      // Let's log...
      var firedRule = session.Query<TemperatureRule>().FirstOrDefault(r => r.Id == eventArgs.Rule.Name);
      var weather = session.Query<CurrentWeatherData>().FirstOrDefault();

      //var entity = await tableStorageService.GetEntityAsync<MiniSplitLogActivity>("minisplitevents", miniSplitRuntimeLog.PartitionKey, miniSplitRuntimeLog.RowKey);
      //if (entity is null)
      //{
      //  return;
      //}

      miniSplitRuntimeLog.FiredRule = true;
      miniSplitRuntimeLog.FiredRuleId = firedRule?.Id;
      miniSplitRuntimeLog.FiredRuleThreshold = firedRule?.Threshhold;
      miniSplitRuntimeLog.Notes = $"Rule fired: {eventArgs.Rule.Name}";

      await tableStorageService.UpdateEntityAsync("minisplitlogs", miniSplitRuntimeLog);

    }




  }





}