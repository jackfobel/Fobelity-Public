using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.Edge.Abstractions;
public sealed class AzureFaceOptions
{
  public string? Endpoint { get; init; }
  public string? Key { get; init; }
  public string PersonGroupId { get; init; } = "shop";
}
