using Azure.Core;
using Azure.Identity;
using System.Net.Http.Headers;

namespace Fobelity.Home.Automation.Adapters.Tuya;

// Chooses where the token comes from
public interface IAccessTokenSource { Task<string?> GetAsync(CancellationToken ct); }

public sealed class StaticTokenSource(string? token) : IAccessTokenSource
{
  public Task<string?> GetAsync(CancellationToken ct) => Task.FromResult(token);
}

// Uses Managed Identity or Client Secret if present
public sealed class EntraTokenSource : IAccessTokenSource
{
  private readonly TokenCredential _cred;
  private readonly string _scope; // e.g. "api://<mini-split-api-app-id>/.default"

  public EntraTokenSource(TokenCredential cred, string scope) => (_cred, _scope) = (cred, scope);

  public async Task<string?> GetAsync(CancellationToken ct)
  {
    var ctx = new TokenRequestContext(new[] { _scope });
    var tok = await _cred.GetTokenAsync(ctx, ct);
    return tok.Token;
  }
}

// DelegatingHandler that adds Authorization: Bearer <token>
public sealed class BearerHandler(IAccessTokenSource src) : DelegatingHandler
{
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
  {
    var token = await src.GetAsync(ct);
    if (!string.IsNullOrWhiteSpace(token))
      req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await base.SendAsync(req, ct);
  }
}
