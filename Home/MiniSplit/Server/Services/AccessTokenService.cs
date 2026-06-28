using DomainModels.Token.Interfaces;
using Microsoft.Identity.Web;

namespace Fobelity.Home.MiniSplit.Server.Services
{
  public class AccessTokenService : IAccessTokenService
  {
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITokenAcquisition _tokenAcquisition;

    public AccessTokenService(
      IHttpContextAccessor httpContextAccessor,
      ITokenAcquisition tokenAcquisition)
    {
      _httpContextAccessor = httpContextAccessor;
      _tokenAcquisition = tokenAcquisition;
    }

    public string? GetAccessTokenForCurrentUser()
    {
      // Optional sync wrapper — not ideal
      return GetAccessTokenForCurrentUserAsync().GetAwaiter().GetResult();
    }

    public async Task<string?> GetAccessTokenForCurrentUserAsync()
    {
      var user = _httpContextAccessor.HttpContext?.User;
      if (user == null || !user.Identity?.IsAuthenticated == true)
      {
        return null;
      }

      try
      {
        var scopes = new[] { "https://sanitized.redacted.com/dellatestapp/.default" };
        var token = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);
        return token;
      }
      catch (Exception ex)
      {
        // Log or handle exception
        //return null;
        throw new InvalidOperationException("Failed to acquire access token for the current user.", ex);
      }
    }
  }



}
