using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace Fobelity.Home.Automation.DeviceHub.Api
{
  public class DevAllowAllHandler : AuthenticationHandler<AuthenticationSchemeOptions>
  {
    public DevAllowAllHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        => Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("role", "Dev") }, "Dev")), "Dev")));
  }
}
