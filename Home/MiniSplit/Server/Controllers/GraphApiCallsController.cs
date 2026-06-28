using Fobelity.Home.MiniSplit.Server.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Fobelity.Home.MiniSplit.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "MiniSplit.UserAccess")]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class GraphApiCallsController : ControllerBase
{
    private readonly MsGraphService _graphApiClientService;

    public GraphApiCallsController(MsGraphService graphApiClientService)
    {
        _graphApiClientService = graphApiClientService;
    }

    [HttpGet]
    public async Task<IEnumerable<string>> Get()
    {
        var userData = await _graphApiClientService.GetGraphApiUser();
        if (userData == null)
            return new List<string> { "no user data" };

        return new List<string> { $"DisplayName: {userData.DisplayName}",
            $"GivenName: {userData.GivenName}", $"AboutMe: {userData.AboutMe}" };
    }
}
