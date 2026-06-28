using Fobelity.Home.MiniSplit.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Fobelity.Home.MiniSplit.Server.Controllers
{
  [ApiController]
  [Route("api/avatar-test")]
  [AllowAnonymous]              // bypass your FallbackPolicy
  [IgnoreAntiforgeryToken]      // your app adds AutoValidateAntiforgeryToken globally
  public class AvatarTestController : ControllerBase
  {
    private readonly IHubContext<UnifiedHub> _hub;
    public AvatarTestController(IHubContext<UnifiedHub> hub) => _hub = hub;

    [HttpPost("say")]
    public async Task<IActionResult> Say([FromQuery] int ms = 1400)
    {
      var payload = new
      {
        type = "speechVisemes",
        id = Guid.NewGuid().ToString(),
        audioUrl = "/tts/hello.wav",   // serve WAV from Client/Server same-origin for dev
        durationMs = ms,
        timeline = new object[] {
        new { t =  40, v = "PP", i = 0.9 },
        new { t = 120, v = "E"  },
        new { t = 210, v = "AA" },
        new { t = 320, v = "O"  },
        new { t = 430, v = "SS" }
      }
      };
      await _hub.Clients.All.SendAsync("speechVisemes", payload);
      return Ok(payload);
    }
  }

}
