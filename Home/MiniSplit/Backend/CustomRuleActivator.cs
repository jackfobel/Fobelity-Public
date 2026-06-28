////using System.Data;
//using NRules.RuleModel;
//using NRules.Fluent;         // RuleRepository, RuleCompiler
//using NRules.Fluent.Dsl;     // Rule base class for defining rules
//using NRules.Extensibility;  // IRuleActivator

//namespace BackendServices
//{
//  public class CustomRuleActivator : IRuleActivator
//  {
//    private readonly IServiceProvider _provider;

//    public CustomRuleActivator(IServiceProvider provider)
//    {
//      _provider = provider;
//    }

//    public IEnumerable<Rule> Activate(Type type)
//    {
//      var instance = (Rule)_provider.GetRequiredService(type);
//      return new[] { instance };
//    }

//  }


//}
