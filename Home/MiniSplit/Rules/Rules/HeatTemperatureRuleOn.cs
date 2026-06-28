using BackendServices;
using ComfortRulesEngine.Base;
using DomainModels.Command.Interfaces;
using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Device.Models;
using DomainModels.Email.Interfaces;
using DomainModels.RulesEngine;
using DomainModels.Storage.Interfaces;
using DomainModels.Storage.Models;
using Microsoft.Extensions.Logging;

namespace ComfortRulesEngine.Rules
{
  public class HeatTemperatureRuleOn : BaseTemperatureRule
  {
    private readonly ITableStorageService _tableStorageService;

    public HeatTemperatureRuleOn(
        ICommandService commandHelper,
        IConfigurationService configurationService,
        IEmailService emailService,
        IDeviceService deviceService,
        ITableStorageService tableStorageService,
        ILogger<BaseTemperatureRule> logger,
        RuntimeTrackingService RuntimeTracker)
      : base(commandHelper,
          configurationService,
          emailService,
          deviceService,
          tableStorageService,
          logger,
          RuntimeTracker)
    {
      _tableStorageService = tableStorageService;
    }


    public override void Define()
    {
      CurrentWeatherData weather = null;
      TemperatureRule tempRule = null;
      DeviceStatus miniSplit = null;
      MiniSplitLogActivity miniSplitLog = null;

      When()
        .Match<TemperatureRule>(() => tempRule, r => r.Id == "heat" && r.Enabled)
        .Match<CurrentWeatherData>(() => weather)
        .Match<DeviceStatus>(() => miniSplit)
        .Match<MiniSplitLogActivity>(() => miniSplitLog)
        .Not<AlreadyProcessedFact>(f => f.RuleId == "HeatOn");

      Filter()
        .Where(() => weather.Temperature < tempRule.Threshhold)
        .Where(() => !miniSplit.Switch || miniSplit.Mode != "heat");

      Then()
        .Do(ctx => LogRuleTriggered(weather, tempRule, miniSplit, miniSplitLog))
        .Do(ctx => ExecuteActionOn(miniSplit, weather, tempRule, "heat", miniSplitLog))
        .Do(ctx => ctx.Insert(new AlreadyProcessedFact("HeatOn")));
    }






  }
}