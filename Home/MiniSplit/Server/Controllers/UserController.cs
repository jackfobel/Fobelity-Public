using Fobelity.Home.MiniSplit.Shared.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Security.Claims;

namespace Fobelity.Home.MiniSplit.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "MiniSplit.UserAccess")]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class UserController : ControllerBase
{
  [HttpGet]
  [AllowAnonymous]
  public IActionResult GetCurrentUser() => Ok(CreateUserInfo(User));

  private UserInfo CreateUserInfo(ClaimsPrincipal claimsPrincipal)
  {
    if (!claimsPrincipal?.Identity?.IsAuthenticated ?? true)
    {
      return UserInfo.Anonymous;
    }

    var userInfo = new UserInfo
    {
      IsAuthenticated = true
    };

    if (claimsPrincipal?.Identity is ClaimsIdentity claimsIdentity)
    {

      //////// 🔍 Output claims to console for inspection
      //////Console.WriteLine("🔐 User Claims:");
      //////foreach (var claim in claimsIdentity.Claims)
      //////{
      //////  Console.WriteLine($"🔸 {claim.Type}: {claim.Value}");
      //////}

      // This is the key to returning claims properly.
      userInfo = new UserInfo
      {
        IsAuthenticated = claimsPrincipal.Identity?.IsAuthenticated ?? false,
        NameClaimType = ClaimTypes.Name, // or "name" if your claim uses that
        RoleClaimType = ClaimTypes.Role,
        Claims = claimsPrincipal.Claims
              .Select(c => new ClaimValue(c.Type, c.Value))
              .ToList()
      };

      // @context?.User?.Identity?.Name was coming back null in MainLayout.razor
      userInfo.NameClaimType = claimsIdentity.NameClaimType;
    }
    else
    {
      userInfo.NameClaimType = ClaimTypes.Name;
      userInfo.RoleClaimType = ClaimTypes.Role;
    }

    if (claimsPrincipal?.Claims?.Any() ?? false)
    {
      var claims = claimsPrincipal.Claims.Select(u => new ClaimValue(u.Type, u.Value))
                                            .ToList();


      userInfo.Claims = claims;
    }

    return userInfo;
  }
}
