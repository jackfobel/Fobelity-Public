using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiniSplitControlService.Controllers
{
  [ApiController]
  [Authorize(Roles = "MiniSplit.Controller,MiniSplit.Scheduler,MiniSplit.UserAccess,API.Invoker")]
  [Route("api/[controller]")]
  public class StatusController : ControllerBase
  {
    [HttpGet]
    public IActionResult Get()
    {
      var claims = User.Claims.Select(c => $"{c.Type} = {c.Value}");
      Console.WriteLine("🔐 Incoming Claims:");
      foreach (var claim in claims)
      {
        Console.WriteLine(claim);
      }

      Console.WriteLine("Authorization Header: {Header}", Request.Headers["Authorization"].ToString());


      return Ok(new { claims });

    }
  }

}
