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
  public class CoolTemperatureRuleOff : BaseTemperatureRule
  {
    private readonly ITableStorageService _tableStorageService;

    public CoolTemperatureRuleOff(
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
        .Not<AlreadyProcessedFact>(f => f.RuleId == "CoolOff");
        //.Match<RuleCooldownStatus>(() => cooldown, c => c.RuleId == "CoolOff" && c.ShouldFire);


      Filter()
        .Where(() => weather.Temperature < tempRule.Threshhold)
        .Where(() => miniSplit.Switch && miniSplit.Mode == "cold")

        // TODO: Test and validate.
        // We don't want to turn off the AC if I am currently in the shop and set temp to 61F.
        .Where(() => !(miniSplit.TempSet == 610 && weather.Temperature < tempRule.Threshhold));

      Then()
        .Do(ctx => LogRuleTriggered(weather, tempRule, miniSplit, miniSplitLog))
        .Do(ctx => ExecuteActionOff(miniSplit, weather, tempRule, "cold", miniSplitLog))
        .Do(ctx => ctx.Insert(new AlreadyProcessedFact("CoolOff")));
    }






  }
}
