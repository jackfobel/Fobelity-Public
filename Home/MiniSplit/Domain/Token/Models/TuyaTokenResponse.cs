using DomainModels.Token.Interfaces;

namespace DomainModels.Token.Models
{
  public class TuyaTokenResponse : ITuyaTokenResponse
  {
    public TuyaTokenResult result { get; set; }
    public bool success { get; set; }
    public long t { get; set; }
    public string tid { get; set; }
    //public string? timestamp { get; set; }
  }
}
