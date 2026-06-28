using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComfortRulesEngine.Base
{
  public class AlreadyProcessedFact
  {
    public string RuleId { get; }

    public AlreadyProcessedFact(string ruleId)
    {
      RuleId = ruleId;
    }
  }
}
