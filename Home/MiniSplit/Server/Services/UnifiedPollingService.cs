using DomainModels.Device.Models;
using Fobelity.Home.MiniSplit.Domain.Token.Interfaces;
using Fobelity.Home.MiniSplit.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Service.Hubs
{
  public class UnifiedPollingService : BackgroundService
  {
    private readonly ILogger<UnifiedPollingService> _logger;
    private readonly IHubContext<UnifiedHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;

    public UnifiedPollingService(
      ILogger<UnifiedPollingService> logger,
      IServiceScopeFactory scopeFactory,
      IHubContext<UnifiedHub> hubContext)
    {
      _logger = logger;
      _scopeFactory = scopeFactory;
      _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        // Ensure we are not simply polling when no clients are connected.
        if (UnifiedHub.ConnectedClients > 0)
        {
          using var scope = _scopeFactory.CreateScope();
          var apiServer = scope.ServiceProvider.GetRequiredService<IApiServer>();

          try
          {
            var status = await apiServer.GetMiniSplitStatusAsync();

            if (status is null)
            {
              _logger.LogWarning("⚠️ Failed to retrieve IoT device status from API.");
            }
            else
            {
              _logger.LogInformation("📡 Broadcasting status to {ClientCount} clients", UnifiedHub.ConnectedClients);
              await _hubContext.Clients.All.SendAsync("MiniSplitReceiveStatusUpdate", status, cancellationToken: stoppingToken);
            }
          }
          catch (Exception ex)
          {
            _logger.LogError(ex, "❌ Error during polling execution");
          }
        }
        else
        {
          _logger.LogDebug("⏸ No connected clients. Skipping polling.");
        }

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
      }
    }
  }
}
