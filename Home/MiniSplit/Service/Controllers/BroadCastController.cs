//using DomainModels.Device.Models;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.AspNetCore.SignalR;

//namespace Fobelity.Home.MiniSplit.Service.Controllers
//{
//  public class BroadCastController : Controller
//  {
//    [ApiController]
//    [Route("api/test")]
//    [AllowAnonymous]
//    //[Authorize]
//    public class TestController : ControllerBase
//    {
//      private readonly IHubContext<MiniSplitHub> _hub;

//      public TestController(IHubContext<MiniSplitHub> hub)
//      {
//        _hub = hub;
//      }

//      [HttpPost("status")]
//      //[Authorize]
//      [AllowAnonymous]
//      public async Task<IActionResult> BroadcastMiniSplitStatus()
//      {
//        var testStatus = new DeviceStatus
//        {
//          Switch = true,
//          Mode = "Cool",
//          TempSetF = 880,
//          TempCurrentF = 790,
//        };

//        await _hub.Clients.All.SendAsync("MiniSplitReceiveStatusMessage", testStatus);
//        return Ok("Status broadcasted.");
//      }
//    }

//  }
//}
