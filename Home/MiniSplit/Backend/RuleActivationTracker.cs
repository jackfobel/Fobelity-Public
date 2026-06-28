using NRules.RuleModel;
using NRules.Diagnostics;
using NRules;
using System.Collections.Concurrent;

namespace BackendServices
{

  public class RuleActivationTracker
  {
    private readonly ConcurrentBag<IRuleDefinition> _activatedRules = new();
    private readonly ConcurrentBag<IRuleDefinition> _firedRules = new();
    private readonly ConcurrentBag<(IRuleDefinition Rule, IEnumerable<object> Facts)> _activatedFacts = new();

    public void OnActivationCreated(object sender, AgendaEventArgs e)
    {
      Console.WriteLine("OnActivationCreated.");
      _activatedRules.Add(e.Rule);

      var facts = e.Match.Facts.Select(f => f.Value).ToList();
      _activatedFacts.Add((e.Rule, facts));
    }

    public void OnRuleFired(object sender, AgendaEventArgs e)
    {
      Console.WriteLine("OnRuleFired.");
      _firedRules.Add(e.Rule);
    }

    public IEnumerable<IRuleDefinition> GetActivatedRules() => _activatedRules;
    public IEnumerable<IRuleDefinition> GetFiredRules() => _firedRules;
    public IEnumerable<(IRuleDefinition Rule, IEnumerable<object> Facts)> GetActivatedFacts() => _activatedFacts;
  }



}
