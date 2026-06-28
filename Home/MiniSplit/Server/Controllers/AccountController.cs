using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fobelity.Home.MiniSplit.Server.Controllers;

// orig src https://github.com/berhir/BlazorWebAssemblyCookieAuth
[Route("api/[controller]")]
[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class AccountController : ControllerBase
{

  [HttpGet("Login")]
  [AllowAnonymous] // Don't protect this — it's for redirecting
  public ActionResult Login(string? returnUrl, string? claimsChallenge)
  {
    // var claims = "{\"access_token\":{\"acrs\":{\"essential\":true,\"value\":\"c1\"}}}";
    // var claims = "{\"id_token\":{\"acrs\":{\"essential\":true,\"value\":\"c1\"}}}";

    var properties = GetAuthProperties(returnUrl);

    if (claimsChallenge != null)
    {
      string jsonString = claimsChallenge.Replace("\\", "")
          .Trim(new char[1] { '"' });

      properties.Items["claims"] = jsonString;
    }

    return Challenge(properties);
  }

  [HttpPost("Logout")]
  [Authorize(Roles = "MiniSplit.UserAccess")]
  [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
  public IActionResult Logout()
  {
      return SignOut(
          new AuthenticationProperties { RedirectUri = "/" },
          CookieAuthenticationDefaults.AuthenticationScheme,
          OpenIdConnectDefaults.AuthenticationScheme);
  }

    /// <summary>
    /// Original src:
    /// https://github.com/dotnet/blazor-samples/blob/main/8.0/BlazorWebOidc/BlazorWebOidc/LoginLogoutEndpointRouteBuilderExtensions.cs
    /// </summary>
    private static AuthenticationProperties GetAuthProperties(string? returnUrl)
    {
        const string pathBase = "/";

        // Prevent open redirects.
        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = pathBase;
        }
        else if (!Uri.IsWellFormedUriString(returnUrl, UriKind.Relative))
        {
            returnUrl = new Uri(returnUrl, UriKind.Absolute).PathAndQuery;
        }
        else if (returnUrl[0] != '/')
        {
            returnUrl = $"{pathBase}{returnUrl}";
        }

        return new AuthenticationProperties { RedirectUri = returnUrl };
    }
}
