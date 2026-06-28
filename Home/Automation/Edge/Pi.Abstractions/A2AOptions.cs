using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.Edge
{
  public sealed class A2AOptions
  {
    public string BaseUrl { get; set; } = "https://YOUR-A2A-SERVER.example.com/";
    public string SayEndpoint { get; set; } = "http://127.0.0.1:8080/say";
    public bool AppendDryRunPrefix { get; set; } = false;
  }
}
