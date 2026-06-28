namespace Fobelity.Home.Automation.Edge
{
  public sealed class AvatarOptions
  {
    public string Url { get; set; } = "https://YOUR-AVATAR-HOST:5111";
    public string? ApiKey { get; set; }           // if you add auth later
    public string? DefaultClientId { get; set; } = "Kiosk-01"; // e.g., "Kiosk-01"
    public bool UseHub { get; set; } = false;     // keep false for now
  }

}
