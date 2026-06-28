using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Fobelity.Home.Automation.Edge
{
  public sealed class AvatarHubSpeak : BackgroundService, IAvatarSpeak
  {
    private readonly AvatarOptions _opt;
    private HubConnection? _hub;

    public AvatarHubSpeak(IOptions<AvatarOptions> opt) => _opt = opt.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
      _hub = new HubConnectionBuilder()
        .WithUrl($"{_opt.Url.TrimEnd('/')}/avatarHub", o =>
        {
#if DEBUG
          o.HttpMessageHandlerFactory = _ => new HttpClientHandler
          {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
          };
#endif
        })
        .WithAutomaticReconnect()
        .Build();

      await _hub.StartAsync(ct);
      await Task.Delay(Timeout.Infinite, ct);
    }

    public Task<bool> SpeakAsync(string text, string? voice = null, string? clientId = null, CancellationToken ct = default)
      => (_hub is { State: HubConnectionState.Connected })
         ? _hub.InvokeAsync("Say", new { Text = text, Voice = voice, ClientId = clientId ?? _opt.DefaultClientId }, ct)
              .ContinueWith(t => t.IsCompletedSuccessfully, ct)
         : Task.FromResult(false);
  }

}

