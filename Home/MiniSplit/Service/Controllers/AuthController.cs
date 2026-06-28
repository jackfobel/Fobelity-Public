using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiniSplitControlService.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize]
  public class AuthController : ControllerBase
  {
    [HttpGet("me")]
    public IActionResult Me()
    {
      if (User?.Identity?.IsAuthenticated == true)
      {
        var userDetails = new
        {
          User.Identity.Name,
          Claims = User.Claims.Select(c => new { c.Type, c.Value })
        };

        return Ok(userDetails);
      }

      return Unauthorized();
    }

    [HttpGet("cors")]
    public IActionResult Cors()
    {
      return Ok();
    }

    [HttpGet("secure")]
    public IActionResult Secure()
    {
      return Ok("🔐 You're authenticated and authorized!");
    }

  }

}
