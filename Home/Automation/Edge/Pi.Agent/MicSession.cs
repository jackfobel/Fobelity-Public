using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Serilog;

namespace Fobelity.Home.Automation.Edge
{
  public sealed class MicSession : IDisposable
  {
    public AudioConfig Audio { get; }
    public SpeechRecognizer Recognizer { get; }

    public MicSession(AudioConfig audio, SpeechRecognizer recognizer)
    {
      Audio = audio;
      Recognizer = recognizer;
    }

    public void Dispose()
    {
      try { Recognizer.Dispose(); } catch { }
      try { Audio.Dispose(); } catch { }
    }
  }


}
