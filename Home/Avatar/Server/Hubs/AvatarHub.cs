using Avatar.Models;
using Microsoft.AspNetCore.SignalR;

namespace Avatar.Hubs;

// AvatarHub.cs
// Avatar.Server/Hubs/AvatarHub.cs
public class AvatarHub : Hub
{
  public static int ConnectedCount;

  public override async Task OnConnectedAsync()
  {
    Interlocked.Increment(ref ConnectedCount);
    var clientId = Context.GetHttpContext()?.Request.Query["clientId"].ToString();
    if (!string.IsNullOrWhiteSpace(clientId))
      await Groups.AddToGroupAsync(Context.ConnectionId, clientId);
    await base.OnConnectedAsync();
  }

  public override async Task OnDisconnectedAsync(Exception? ex)
  {
    Interlocked.Decrement(ref ConnectedCount);
    await base.OnDisconnectedAsync(ex);
  }

  // Optional: a server-side method the agent can call (stronger than raw broadcast)
  public Task Say(SpeakRequest req)
  {
    if (req is null) return Task.CompletedTask;

    var payload = new { text = req.Text, ssml = req.Ssml, voice = req.Voice, animation = req.Animation };

    if (!string.IsNullOrWhiteSpace(req.ClientId))
      return Clients.Group(req.ClientId).SendAsync("speak", payload);

    return Clients.All.SendAsync("speak", payload);
  }
}
