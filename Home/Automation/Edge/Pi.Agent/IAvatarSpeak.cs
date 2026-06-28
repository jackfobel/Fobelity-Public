namespace Fobelity.Home.Automation.Edge
{
  public interface IAvatarSpeak
  {
    Task<bool> SpeakAsync(string text, string? voice = null, string? clientId = null, CancellationToken ct = default);
  }
}
