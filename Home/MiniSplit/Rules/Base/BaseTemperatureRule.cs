using ComfortRulesEngine.Rules;
using DomainModels.Command.Interfaces;
using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Device.Models;
using DomainModels.Email.Interfaces;
using DomainModels.RulesEngine;
using DomainModels.Storage.Interfaces;
using Microsoft.Extensions.Logging;
using NRules.Fluent.Dsl;
using BackendServices;
using Newtonsoft.Json;

namespace ComfortRulesEngine.Base
{
  public abstract class BaseTemperatureRule : Rule
  {
    protected ICommandService CommandHelper { get; }
    protected IConfigurationService ConfigurationService { get; }
    protected IEmailService EmailService { get; }
    protected IDeviceService DeviceService { get; }
    protected ILogger<BaseTemperatureRule> Logger;
    protected ITableStorageService TableStorageService;
    protected RuntimeTrackingService RuntimeTracker;

    protected BaseTemperatureRule(
        ICommandService commandHelper,
        IConfigurationService configurationService,
        IEmailService emailService,
        IDeviceService deviceService,
        ITableStorageService tableStorageService,
        ILogger<BaseTemperatureRule> logger,
        RuntimeTrackingService runtimeTracker)
    {
      CommandHelper = commandHelper;
      ConfigurationService = configurationService;
      EmailService = emailService;
      DeviceService = deviceService;
      TableStorageService = tableStorageService;
      Logger = logger;
      RuntimeTracker = runtimeTracker;
    }

    protected async Task TurnOnMiniSplit(
      DeviceStatus miniSplit, 
      string mode, 
      CurrentWeatherData weather, 
      TemperatureRule tempRule,
      MiniSplitLogActivity miniSplitLog)
    {
      miniSplit.Switch = true;
      miniSplit.Mode = mode;

      var jsonPayload = new
      {
        commands = new[]
          {
              new { code = "switch", value = true }
          }
      };

      // TODO: Set to Cold or Heat?

      Logger.LogInformation($"TurnOnMiniSplit called using mode: {mode} based on CurrentWeathData.Temperature/Humidity: {weather.Temperature}/{weather.Humidity}");

      await RuntimeTracker.TrackMiniSplitRuntimeAsync("minisplit");

      // Temporarily commented out.
      // Also, dont use this status for current minisplit status...
      var updateStatus = await CommandHelper.SendCommandPost<DeviceAccessResponse>(
          "SendDeviceAction",
          ConfigurationService.IoTDeviceId,
          DeviceService,
          jsonPayload
      );


      // This is the current status.
      var iotDeviceStatus = await CommandHelper.SendCommand<IoTDeviceStatus>(
        "GetDeviceStatus",
        ConfigurationService.IoTDeviceId,
        DeviceService);



      string iotDeviceStatusJson = JsonConvert.SerializeObject(iotDeviceStatus);
      var deviceStatusAfterTurningOn = DeviceStatusParser.Parse(iotDeviceStatusJson);
      //
      //// This is for testing only... while the above is commented out.
      //var deviceStatusAfterTurningOn = new DeviceStatus();
      //deviceStatusAfterTurningOn.HumidityCurrent = -1;
      //deviceStatusAfterTurningOn.TempCurrentF = -1;


      // Wait for 5 seconds to allow device state to update
      await Task.Delay(TimeSpan.FromSeconds(5));

      miniSplitLog.IsOn = true;
      miniSplitLog.WasOn = false;
      miniSplitLog.Notes = "Turned on the Mini-split.";
      miniSplitLog.FiredRuleThreshold = GetThreshold(tempRule);
      miniSplitLog.FiredRuleId = tempRule.Id;
      miniSplitLog.InsideHumidity = deviceStatusAfterTurningOn.HumidityCurrent;
      miniSplitLog.InsideTempF = deviceStatusAfterTurningOn.TempCurrentF;
      miniSplitLog.TempSet = deviceStatusAfterTurningOn.TempSetF;

      await TableStorageService.UpsertEntityAsync("minisplitlogs", miniSplitLog);

      await EmailService.SendEmailAsync(
        subject: "Mini-Split Turned ON",
        body: $@"
          <html>
            <body style='font-family:sans-serif;'>
              <h2>Mini-Split Activated</h2>
              <h3>Threshold Met: {GetThreshold(tempRule)}</h3>
              <p><strong>Mode:</strong> {mode}</p>
              <p><strong>Outside Temperature:</strong> {weather.Temperature}°F</p>
              <p><strong>Outside Humidity:</strong> {weather.Humidity}%</p>
              <p>Timestamp: {DateTime.UtcNow}</p>
            </body>
          </html>");

      Logger.LogInformation("Logged runtime stats to MiniSplitLogs table from TurnOnMiniSplit.");

    }

    protected async Task TurnOffMiniSplit(
      DeviceStatus miniSplit, 
      string mode, 
      CurrentWeatherData weather,
      TemperatureRule tempRule,
      MiniSplitLogActivity miniSplitLog)
    {
      miniSplit.Switch = true;
      miniSplit.Mode = mode;

      var jsonPayload = new
      {
        commands = new[]
          {
              new { code = "switch", value = false }
          }
      };

      Logger.LogInformation($"TurnOffMiniSplit called using mode: {mode} based on CurrentWeathData.Temperature/Humidity: {weather.Temperature}/{weather.Humidity}");

      miniSplitLog.IsOn = false;
      miniSplitLog.WasOn = true;
      miniSplitLog.Notes = "Turned off the Mini-split.";
      miniSplitLog.FiredRuleThreshold = GetThreshold(tempRule);
      miniSplitLog.FiredRuleId = tempRule.Id;
      miniSplitLog.InsideHumidity = miniSplit.HumidityCurrent;
      miniSplitLog.InsideTempF = miniSplit.TempCurrentF;
      miniSplitLog.TempSet = miniSplit.TempSetF;

      await RuntimeTracker.TrackMiniSplitRuntimeAsync("minisplit");

      // Temporarily commented out.
      // Uncomment this to go live !!!
      // Same with the TurnOnMiniSPlit method...
      var updateStatus = await CommandHelper.SendCommandPost<DeviceAccessResponse>(
          "SendDeviceAction",
          ConfigurationService.IoTDeviceId,
          DeviceService,
          jsonPayload
      );


      // This is the current status.
      var iotDeviceStatus = await CommandHelper.SendCommand<IoTDeviceStatus>(
        "GetDeviceStatus",
        ConfigurationService.IoTDeviceId,
        DeviceService);

      string iotDeviceStatusJson = JsonConvert.SerializeObject(iotDeviceStatus);
      var deviceStatusAfterTurningOff = DeviceStatusParser.Parse(iotDeviceStatusJson);


      await TableStorageService.UpsertEntityAsync("minisplitlogs", miniSplitLog);

      await EmailService.SendEmailAsync(
        subject: "Mini-Split Turned OFF",
        body: $@"
                <html>
                  <body style='font-family:sans-serif;'>
                    <h2>Mini-Split Deactivated</h2>
                    <h3>Threshold Met: {GetThreshold(tempRule)}</h3>
                    <p><strong>Mode:</strong> {mode}</p>
                    <p><strong>Outside Temperature:</strong> {weather.Temperature}°F</p>
                    <p><strong>Outside Humidity:</strong> {weather.Humidity}%</p>
                    <p>Timestamp: {DateTime.UtcNow}</p>
                  </body>
                </html>");

      Logger.LogInformation("Logged runtime stats to MiniSplitLogs table from TurnOnMiniSplit.");
    }

    // You can use the following to interact with rules fired.
    // Put this in your code where you implement this class.
    //session.Events.RuleFiringEvent += OnRuleFiringEvent;
    //public static void OnRuleFiringEvent(object sender, AgendaEventArgs e)
    //{
    //  Console.WriteLine("Rule about to fire: {0}", e.Rule.Name);
    //  var session = (NRules.ISession)sender;
    //  var facts = session.Query<WeatherData>().ToList();
    //  foreach (var fact in facts)
    //  {
    //    Console.WriteLine("WeatherData - Temperature: {0}, Humidity: {1}", fact.Temperature, fact.Humidity);
    //  }
    //}


    //protected bool GetEnabled(TemperatureRule tempRule)
    //{

    //  if (tempRule == null || tempRule?.Enabled == null)
    //  {
    //    throw new ArgumentNullException("tempRule or tempRule.Enabled is null.");
    //  }

    //  Logger.LogInformation($"Is this Rule enabled? {tempRule.Enabled} for Id: {tempRule.Id}");

    //  return tempRule.Enabled;
    //}

    protected bool GetEnabled(TemperatureRule tempRule, bool log = false)
    {
      if (log)
      {
        Logger.LogInformation($"Is this Rule enabled? {tempRule.Enabled} for Id: {tempRule.Id}");
      }

      return tempRule.Enabled;
    }

    protected void LogEvaluation(TemperatureRule rule, CurrentWeatherData weather, DeviceStatus miniSplit)
    {
      Logger.LogInformation("Rule Trigger Check → Rule Id: {0}, Enabled: {1}, Threshold: {2}",
          rule.Id, rule.Enabled, rule.Threshhold);
      Logger.LogInformation("Weather → Temp: {0}, Humidity: {1}", weather.Temperature, weather.Humidity);
      Logger.LogInformation("MiniSplit → Mode: {0}, IsOn: {1}", miniSplit.Mode, miniSplit.Mode);
    }

    protected string GetMode(DeviceStatus miniSplitStatus)
    {

      if (miniSplitStatus == null || miniSplitStatus?.Mode == null)
      {
        throw new ArgumentNullException("miniSplitStatus or miniSplitStatus.Mode is null.");
      }

      Logger.LogInformation($"What Mode is the Mini-split in? {miniSplitStatus.Mode} with Status: {miniSplitStatus.Switch}");

      return miniSplitStatus.Mode;
    }

    protected bool IsOnAlready(DeviceStatus miniSplitStatus)
    {

      if (miniSplitStatus == null || miniSplitStatus?.Switch == null)
      {
        throw new ArgumentNullException("miniSplitStatus or miniSplitStatus.IsOn is null.");
      }

      Logger.LogInformation($"Is Mini-split already on? {miniSplitStatus.Switch} for Mode: {miniSplitStatus.Mode}.");

      return miniSplitStatus.Switch;
    }

    protected int GetThreshold(TemperatureRule tempRule)
    {
      if (tempRule == null || tempRule?.Threshhold == null)
      {
        throw new ArgumentNullException("tempRule or tempRule.Threshhold is null.");
      }

      Logger.LogInformation($"What is the temperature threshhold? {tempRule.Threshhold} for Id: {tempRule.Id}");

      return tempRule.Threshhold;
    }

    protected void LogRuleTriggered(CurrentWeatherData weather, TemperatureRule tempRule, DeviceStatus miniSplit, MiniSplitLogActivity miniSplitLog)
    {
      Logger.LogInformation($"Rule triggered with Outside Temp: {weather?.Temperature}, Temperature Threshhold: {tempRule?.Threshhold} - What Mode is Mini-split in? {miniSplit?.Mode} - Is Mini-split already on? {miniSplit?.Switch} -end");
    }

    protected async Task ExecuteActionOn(DeviceStatus miniSplit, CurrentWeatherData weather, TemperatureRule tempRule, string mode, MiniSplitLogActivity miniSplitLog)
    {
      Logger.LogInformation($"What is the temperature threshhold? {tempRule.Threshhold} for Id: {tempRule.Id}");

      await TurnOnMiniSplit(miniSplit, mode, weather, tempRule, miniSplitLog);
    }

    protected async Task ExecuteActionOff(DeviceStatus miniSplit, CurrentWeatherData weather, TemperatureRule tempRule, string mode, MiniSplitLogActivity miniSplitLog)
    {
      Logger.LogInformation($"What is the temperature threshhold? {tempRule.Threshhold} for Id: {tempRule.Id}");

      await TurnOffMiniSplit(miniSplit, mode, weather, tempRule, miniSplitLog);
    }

    protected bool IsManualOverrideActive(DeviceStatus miniSplit, CurrentWeatherData weather, TemperatureRule tempRule)
    {
      //return miniSplit.TempSet == 61 && weather.Temperature > 90;
      return miniSplit.TempSet == 61 && weather.Temperature > tempRule.Threshhold;
    }



  }



}