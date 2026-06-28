namespace Avatar.Models
{
  // Avatar.Server/Models/SpeakText.cs
  public sealed class SpeakText
  {
    public string? Text { get; set; }
    public string? Ssml { get; set; }   // add
    public string? Voice { get; set; }
    public string? ClientId { get; set; }
  }


}
