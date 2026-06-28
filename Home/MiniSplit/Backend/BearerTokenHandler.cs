using Azure.Core;

namespace BackendServices
{
  public class BearerTokenHandler : DelegatingHandler
  {
    private readonly TokenCredential _credential;
    private readonly string _scope;
    private readonly ILogger<BearerTokenHandler> _logger;

    public BearerTokenHandler(TokenCredential credential, string scope, ILogger<BearerTokenHandler> logger)
    {
      _credential = credential;
      _scope = scope;
      _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
      _logger.LogInformation("Requesting access token for scope: {Scope}", _scope);

      var tokenRequestContext = new TokenRequestContext(new[] { _scope },
          claims: "{\"access_token\":{\"xms_cc\":{\"values\":[\"ManagedIdentity.Access\"]}}}");

      var token = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
      _logger.LogInformation("Obtained token (partial): {TokenPrefix}...", token.Token.Substring(0, 20));

      request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);
      return await base.SendAsync(request, cancellationToken);
    }
  }


}
