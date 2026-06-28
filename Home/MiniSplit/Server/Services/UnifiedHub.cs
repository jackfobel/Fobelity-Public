using DomainModels.Device.Models;
using Fobelity.Home.MiniSplit.Domain.Chat.Interfaces;
using Fobelity.Home.MiniSplit.Domain.Chat.Models;
using Microsoft.AspNetCore.SignalR;
using static Fobelity.Home.MiniSplit.Client.Components.Chat;

namespace Fobelity.Home.MiniSplit.Server.Services
{
  public class UnifiedHub : Hub
  {
    private readonly IServiceProvider _serviceProvider;
    private static int _connectionCount = 0;
    public static int ConnectedClients => _connectionCount;


    public UnifiedHub(IServiceProvider serviceProvider)
    {
      _serviceProvider = serviceProvider;
    }


    public override Task OnConnectedAsync()
    {
      Interlocked.Increment(ref _connectionCount);
      return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
      Interlocked.Decrement(ref _connectionCount);
      return base.OnDisconnectedAsync(exception);
    }


    public async Task SendChatterMessage(ChatMessage userMessage)
    {
      await Clients.All.SendAsync("ChatterReceiveMessage", userMessage);

      // Resolve scoped service safely
      using var scope = _serviceProvider.CreateScope();
      var responder = scope.ServiceProvider.GetRequiredService<IAIResponder>();

      var aiMessage = await responder.AskAgentAsync(userMessage);
      await Clients.All.SendAsync("ChatterReceiveMessage", aiMessage);
    }


    public async Task BroadcastMiniSplitStatus(DeviceStatus status)
    {
      await Clients.All.SendAsync("MiniSplitReceiveStatusUpdate", status);
    }



  }
}
