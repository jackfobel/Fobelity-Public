using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.Edge
{
  public sealed class AzureSpeechOptions
  {
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? Key { get; set; }
    public string? Voice { get; set; }
    public string? Device { get; set; }   // playback (you’re using plughw:2,0)
    public string? Mic { get; set; }      // capture device (e.g., plughw:1,0)
    public int GreetCooldownSeconds { get; set; } = 5;
  }

}
