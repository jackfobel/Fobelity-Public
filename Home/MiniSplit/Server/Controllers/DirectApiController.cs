using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fobelity.Home.MiniSplit.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "MiniSplit.UserAccess")]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class DirectApiController : ControllerBase
{
  [HttpGet]
  public IEnumerable<string> Get()
  {
    return new List<string> { "some data", "more data", "loads of data" };
  }

  [HttpGet("getclaims")]
  public IActionResult GetClaims()
  {
    var claims = User.Claims.Select(c => new { c.Type, c.Value });

    // 🔍 Output claims to console for inspection
    Console.WriteLine("🔐 User Claims:");
    foreach (var claim in claims)
    {
      Console.WriteLine($"🔸 {claim.Type}: {claim.Value}");
    }

    return Ok(claims);
  }

  [HttpGet("debug-claims")]
  public IActionResult DebugClaims()
  {
    foreach (var claim in User.Claims)
    {
      Console.WriteLine($"[SERVER] CLAIM: {claim.Type} = {claim.Value}");
    }

    return Ok(User.Claims.Select(c => new { c.Type, c.Value }));
  }

}
