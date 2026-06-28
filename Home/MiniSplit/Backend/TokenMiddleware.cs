using DomainModels.Token.Interfaces;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace BackendServices
{
  public class TokenMiddleware : DelegatingHandler
  {
    private readonly ITuyaTokenService _tokenService;

    public TokenMiddleware(ITuyaTokenService tokenService)
    {
      _tokenService = tokenService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var tokenResponse = await _tokenService.GetToken();
      request.Headers.Add("access_token", tokenResponse.result.access_token); 

      return await base.SendAsync(request, cancellationToken);
    }
  }
}
