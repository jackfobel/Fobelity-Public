namespace DomainModels.Token.Interfaces
{
  public interface ITuyaTokenResult
  {
    string access_token { get; set; }
    int expire_time { get; set; }
    string refresh_token { get; set; }
    string uid { get; set; }
  }
}