using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;


namespace DomainModels.Token
{

  public class AccessTokenHandler : DelegatingHandler
  {
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AccessTokenHandler(IHttpContextAccessor accessor)
    {
      _httpContextAccessor = accessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      var context = _httpContextAccessor.HttpContext;
      if (context is null)
      {
        return await base.SendAsync(request, cancellationToken);
      }

      var token = await context.GetTokenAsync("access_token");
      if (!string.IsNullOrEmpty(token))
      {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
      }

      return await base.SendAsync(request, cancellationToken);
    }

  }

}
