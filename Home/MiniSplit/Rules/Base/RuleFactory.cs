using BackendServices;
using ComfortRulesEngine.Base;
using DomainModels.Command.Interfaces;
using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Email.Interfaces;
using DomainModels.Storage.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ComfortRulesEngine.Rules
{
  public static class RuleFactory
  {
    // Cool - On
    public static CoolTemperatureRuleOn CreateCoolTemperatureRuleOn(IServiceProvider serviceProvider)
    {
      var commandHelper = serviceProvider.GetRequiredService<ICommandService>();
      var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
      var emailService = serviceProvider.GetRequiredService<IEmailService>();
      var deviceService = serviceProvider.GetRequiredService<IDeviceService>();
      var tableStorageService = serviceProvider.GetRequiredService<ITableStorageService>();
      var logger = serviceProvider.GetRequiredService<ILogger<BaseTemperatureRule>>();
      var runtimeTracker = serviceProvider.GetRequiredService<RuntimeTrackingService>();

      return new CoolTemperatureRuleOn(
        commandHelper, 
        configurationService, 
        emailService, 
        deviceService, 
        tableStorageService, 
        logger,
        runtimeTracker);
    }

    // Cool - Off
    public static CoolTemperatureRuleOff CreateCoolTemperatureRuleOff(IServiceProvider serviceProvider)
    {
      var commandHelper = serviceProvider.GetRequiredService<ICommandService>();
      var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
      var emailService = serviceProvider.GetRequiredService<IEmailService>();
      var deviceService = serviceProvider.GetRequiredService<IDeviceService>();
      var tableStorageService = serviceProvider.GetRequiredService<ITableStorageService>();
      var logger = serviceProvider.GetRequiredService<ILogger<BaseTemperatureRule>>();
      var runtimeTracker = serviceProvider.GetRequiredService<RuntimeTrackingService>();

      return new CoolTemperatureRuleOff(
        commandHelper,
        configurationService,
        emailService,
        deviceService,
        tableStorageService,
        logger,
        runtimeTracker);
    }


    // Heat - On
    public static HeatTemperatureRuleOn CreateHeatTemperatureRuleOn(IServiceProvider serviceProvider)
    {
      var commandHelper = serviceProvider.GetRequiredService<ICommandService>();
      var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
      var emailService = serviceProvider.GetRequiredService<IEmailService>();
      var deviceService = serviceProvider.GetRequiredService<IDeviceService>();
      var tableStorageService = serviceProvider.GetRequiredService<ITableStorageService>();
      var logger = serviceProvider.GetRequiredService<ILogger<BaseTemperatureRule>>();
      var runtimeTracker = serviceProvider.GetRequiredService<RuntimeTrackingService>();

      return new HeatTemperatureRuleOn(
        commandHelper,
        configurationService,
        emailService,
        deviceService,
        tableStorageService,
        logger,
        runtimeTracker);
    }

    // Heat - Off
    public static HeatTemperatureRuleOff CreateHeatTemperatureRuleOff(IServiceProvider serviceProvider)
    {
      var commandHelper = serviceProvider.GetRequiredService<ICommandService>();
      var configurationService = serviceProvider.GetRequiredService<IConfigurationService>();
      var emailService = serviceProvider.GetRequiredService<IEmailService>();
      var deviceService = serviceProvider.GetRequiredService<IDeviceService>();
      var tableStorageService = serviceProvider.GetRequiredService<ITableStorageService>();
      var logger = serviceProvider.GetRequiredService<ILogger<BaseTemperatureRule>>();
      var runtimeTracker = serviceProvider.GetRequiredService<RuntimeTrackingService>();

      return new HeatTemperatureRuleOff(
        commandHelper,
        configurationService,
        emailService,
        deviceService,
        tableStorageService,
        logger,
        runtimeTracker);
    }
  }
}
