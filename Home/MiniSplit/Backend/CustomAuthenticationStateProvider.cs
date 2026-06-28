using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authentication;

namespace BackendServices
{
  public class CustomAuthenticationStateProvider : AuthenticationStateProvider
  {
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
      _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
      var user = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
      return Task.FromResult(new AuthenticationState(user));
    }
  }


  public class ClientPrincipal
  {
    public string IdentityProvider { get; set; }
    public string UserId { get; set; }
    public string UserDetails { get; set; }
    public IEnumerable<ClientClaim> Claims { get; set; }

    // ✅ Add this line:
    public string[] Roles { get; set; }
  }

  public class ClientClaim
  {
    public string Typ { get; set; }
    public string Val { get; set; }

    // Optional normalization helper:
    public string Type => Typ;
    public string Value => Val;
  }





}
