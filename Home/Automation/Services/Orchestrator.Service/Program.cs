using Fobelity.Home.Automation.Orchestrator.Service;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(s => s.AddHostedService<OrchestratorWorker>())
    .Build().RunAsync();

// These are just empty Worker Services so the solution compiles/runs while we wire logic later.
public sealed class OrchestratorWorker(ILogger<OrchestratorWorker> log) : BackgroundService
{
  protected override async Task ExecuteAsync(CancellationToken stoppingToken)
  {
    while (!stoppingToken.IsCancellationRequested)
    {
      log.LogInformation("Orchestrator heartbeat {time}", DateTimeOffset.UtcNow);
      await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
    }
  }
}
