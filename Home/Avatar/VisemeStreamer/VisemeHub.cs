using Microsoft.AspNetCore.SignalR;

namespace Fobelity.Home.Avatar.VisemeStreamer;

public class VisemeHub : Hub
{
  private static int _connected;
  public override Task OnConnectedAsync()
  {
    Interlocked.Increment(ref _connected);
    Console.WriteLine($"Client connected. Total: {_connected}");
    return base.OnConnectedAsync();
  }
  public static bool AnyConnected => _connected > 0;
}

