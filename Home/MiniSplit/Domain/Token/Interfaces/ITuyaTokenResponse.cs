using DomainModels.Token.Models;

namespace DomainModels.Token.Interfaces
{
  public interface ITuyaTokenResponse
  {
    TuyaTokenResult result { get; set; }
    bool success { get; set; }
    long t { get; set; }
    string tid { get; set; }
  }
}