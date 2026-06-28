using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MiniSplitControlService.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class HealthController : ControllerBase
  {
    [HttpGet]
    public IActionResult Get() => Ok("Healthy");
  }



}
