using System.Diagnostics;

namespace Fobelity.Home.Automation.Edge.Pi.Agent;

public sealed class CameraService
{
  private readonly string _cmd;
  private readonly string _outputPath;

  public CameraService(IConfiguration cfg)
  {
    _cmd = cfg["Camera:Cmd"] ?? "/usr/bin/rpicam-still";
    _outputPath = cfg["Camera:LastPath"] ?? "/var/lib/fobelity/captures/last.jpg";
    Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);
  }

  public string LastJpegPath => _outputPath;

  public async Task<bool> SnapAsync(int width = 1280, int height = 720, int rotate = 180, bool af = true, bool irNeutralWb = true, int quality = 90, int delayMs = 0, CancellationToken ct = default)
  {
    var tmp = Path.Combine(Path.GetDirectoryName(_outputPath)!, $"tmp_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg");
    var args = new List<string> {
            "-n", "--zsl",
            "--width", width.ToString(), "--height", height.ToString(),
            "--quality", quality.ToString(),
            "--rotation", rotate.ToString(),
            "-o", tmp
        };
    if (af) args.AddRange(new[] { "--autofocus-mode", "continuous", "--autofocus-range", "normal" });
    if (irNeutralWb) args.AddRange(new[] { "--awbgains", "1.0,1.0" });

    if (delayMs > 0) await Task.Delay(delayMs, ct);

    var psi = new ProcessStartInfo(_cmd) { RedirectStandardOutput = true, RedirectStandardError = true };
    foreach (var a in args) psi.ArgumentList.Add(a);
    using var p = Process.Start(psi)!;
    await p.WaitForExitAsync(ct);
    if (p.ExitCode == 0) { File.Move(tmp, _outputPath, true); return true; }
    try { File.Delete(tmp); } catch { }
    return false;
  }
}
