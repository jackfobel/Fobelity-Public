using DomainModels.Token.Models;

namespace DomainModels.Token.Interfaces
{
  public interface ITuyaTokenService
  {
    TuyaTokenResponse CachedToken { get; }
    Task<TuyaTokenResponse> GetToken();
    string GenerateSignature(string clientId, string secret, string timestamp, string url);
    string GenerateProductSignature(string clientId, string secret, string timestamp, string url, string accessToken);
    string GeneratePostProductSignature(string clientId, string secret, string timestamp, string url, string accessToken, string payload);
  }
}
