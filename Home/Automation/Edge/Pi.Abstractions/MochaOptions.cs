using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.Edge
{
  public sealed class MochaOptions
  {
    public string WakeWord { get; set; } = "hey mocha";
    public int WakeWindowSeconds { get; set; } = 7;
    public int ListenTimeoutSeconds { get; set; } = 9;
    public bool RequireConfirmation { get; set; } = false;

    public string? KeywordModelPath { get; set; }  // e.g., "/home/pi/models/hey_mocha.table"

    // silence/latency tuning (ms)
    public int InitialSilenceMs { get; set; } = 12000;   // time to start speaking
    public int EndSilenceMs { get; set; } = 3500;        // pause after last word
    public int PostTtsDelayMs { get; set; } = 400;       // pause after “I’m listening”

    public bool EnableWakeLoop { get; set; } = false;   // toggle background KWS
    public int WakeLoopGapMs { get; set; } = 300;       // pause between windows

    public int KwsFollowSettleMs { get; set; } = 1000; //250; // pause before the follow-up capture

    public int KwsOneShotLingerMs { get; set; } = 900; // max wait after keyword hit for same-breath command

    public int PostGreetSilentListenMs { get; set; } = 2000; // 2s “talk now” window
    public int PostGreetSettleMs { get; set; } = 250;        // small tail settle after TTS

  }

}
