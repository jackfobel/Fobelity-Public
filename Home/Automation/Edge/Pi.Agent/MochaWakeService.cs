using Fobelity.Home.Automation.Edge.Pi.Agent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;


namespace Fobelity.Home.Automation.Edge
{
  public interface IWakeLoopControl
  {
    void CancelCurrentWindow();
    void PauseFor(TimeSpan duration);
  }

  public sealed class MochaWakeService : BackgroundService, IWakeLoopControl
  {
    private readonly MochaVoice _voice;
    private readonly IOptionsMonitor<MochaOptions> _opts;

    private readonly object _sync = new();
    private CancellationTokenSource? _activeWindowCts;
    private long _pausedUntilUtcTicks; // Interlocked-friendly

    public MochaWakeService(MochaVoice voice, IOptionsMonitor<MochaOptions> opts)
    { _voice = voice; _opts = opts; }

    public void CancelCurrentWindow()
    {
      lock (_sync) { _activeWindowCts?.Cancel(); }
    }

    public void PauseFor(TimeSpan duration)
    {
      var until = DateTime.UtcNow.Add(duration).Ticks;
      Interlocked.Exchange(ref _pausedUntilUtcTicks, until);
      CancelCurrentWindow(); // optional, but usually what you want
    }

    private bool IsPaused()
      => DateTime.UtcNow.Ticks < Interlocked.Read(ref _pausedUntilUtcTicks);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      Log.Information("[Wake] service online");

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          if (!_opts.CurrentValue.EnableWakeLoop)
          { await Task.Delay(1000, stoppingToken); continue; }

          if (IsPaused())
          { await Task.Delay(200, stoppingToken); continue; }

          using var windowCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

          lock (_sync) { _activeWindowCts = windowCts; }

          try
          {
            await _voice.ListenWithKeywordAsync(windowCts.Token);
          }
          catch (OperationCanceledException) { /* cancelled by greeter or shutdown */ }
          finally
          {
            lock (_sync)
            {
              if (ReferenceEquals(_activeWindowCts, windowCts))
                _activeWindowCts = null;
            }
          }

          await Task.Delay(Math.Max(100, _opts.CurrentValue.WakeLoopGapMs), stoppingToken);
        }
        catch (OperationCanceledException) { /* normal */ }
        catch (Exception ex)
        {
          Log.Warning(ex, "[Wake] iteration failed");
          await Task.Delay(1000, stoppingToken);
        }
      }

      Log.Information("[Wake] service stopping");
    }
  }

}
