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
using System.Threading.Tasks;

namespace ComfortRulesEngine.Rules
{
  public class CoolTemperatureRuleOn : BaseTemperatureRule
  {
    private readonly ITableStorageService _tableStorageService;

    public CoolTemperatureRuleOn(
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
      //RuleCooldownStatus cooldown = null;

      When()
        .Match<TemperatureRule>(() => tempRule, r => r.Id == "cold" && r.Enabled)
        .Match<CurrentWeatherData>(() => weather)
        .Match<DeviceStatus>(() => miniSplit)
        .Match<MiniSplitLogActivity>(() => miniSplitLog)
        //.Match<RuleCooldownStatus>(() => cooldown, c => c.RuleId == "CoolOn" && c.ShouldFire)
        .Not<AlreadyProcessedFact>(f => f.RuleId == "CoolOn");

      Filter()
        .Where(() => weather.Temperature > tempRule.Threshhold)
        .Where(() => !miniSplit.Switch || miniSplit.Mode != "cold")
        .Where(() => !IsManualOverrideActive(miniSplit, weather, tempRule));

      Then()
        .Do(ctx => LogEvaluation(tempRule, weather, miniSplit))
        .Do(ctx => ExecuteActionOn(miniSplit, weather, tempRule, "cold", miniSplitLog))
        .Do(ctx => ctx.Insert(new AlreadyProcessedFact("CoolOn")));
    }






  }
}
