using Fobelity.Home.Automation.Presence.Service;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(s => s.AddHostedService<PresenceWorker>())
    .Build().RunAsync();

// These are just empty Worker Services so the solution compiles/runs while we wire logic later.
public sealed class PresenceWorker(ILogger<PresenceWorker> log) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      log.LogInformation("Presence heartbeat {time}", DateTimeOffset.UtcNow);
      await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    }
  }
}
