using ComfortRulesEngine.Rules;
using NRules.Fluent;
using NRules.Fluent.Dsl;

namespace ComfortRulesEngine.Base
{
  public class CustomRuleActivator : IRuleActivator
  {
    private readonly IServiceProvider _serviceProvider;

    public CustomRuleActivator(IServiceProvider serviceProvider)
    {
      _serviceProvider = serviceProvider;
    }

    public IEnumerable<Rule> Activate(Type type)
    {
      // Cool
      if (type == typeof(CoolTemperatureRuleOn))
      {
        yield return RuleFactory.CreateCoolTemperatureRuleOn(_serviceProvider);
      }
      else if (type == typeof(CoolTemperatureRuleOff))
      {
        yield return RuleFactory.CreateCoolTemperatureRuleOff(_serviceProvider);
      }

      // Heat
      else if (type == typeof(HeatTemperatureRuleOn))
      {
        yield return RuleFactory.CreateHeatTemperatureRuleOn(_serviceProvider);
      }
      else if (type == typeof(HeatTemperatureRuleOff))
      {
        yield return RuleFactory.CreateHeatTemperatureRuleOff(_serviceProvider);
      }

      else
      {
        yield return (Rule)Activator.CreateInstance(type);
      }
    }
  }


}
