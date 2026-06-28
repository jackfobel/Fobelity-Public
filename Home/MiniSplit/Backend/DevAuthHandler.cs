using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace BackendServices
{
  public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
  {
    public DevAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock) { }

    //protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    //{
    //  var identity = new ClaimsIdentity(new[]
    //  {
    //        new Claim(ClaimTypes.Name, "LocalDev"),
    //        new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "MiniSplit.Scheduler")
    //    }, "DevScheme");

    //  var principal = new ClaimsPrincipal(identity);
    //  var ticket = new AuthenticationTicket(principal, "DevScheme");

    //  return Task.FromResult(AuthenticateResult.Success(ticket));
    //}

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      var identity = new ClaimsIdentity(new[]
      {
        new Claim(ClaimTypes.Name, "LocalDev"),
        new Claim(ClaimTypes.Role, "MiniSplit.UserAccess"),
        new Claim(ClaimTypes.Role, "MiniSplit.Scheduler")
      }, "DevScheme");

      var principal = new ClaimsPrincipal(identity);
      var ticket = new AuthenticationTicket(principal, "DevScheme");

      return Task.FromResult(AuthenticateResult.Success(ticket));
    }

  }

}
