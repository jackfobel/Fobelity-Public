using DomainModels.Device.Models;
using Fobelity.Home.MiniSplit.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Fobelity.Home.MiniSplit.Client.Pages
{
  public partial class Status
  {
    public bool IsInitialized { get; private set; }
    public bool IsMiniSplitRunning { get; private set; }
    public DeviceStatus? MiniSplitStatus { get; private set; }

    [Inject] public AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] public IAntiforgeryHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] public ILogger<Status> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
      if (IsInitialized)
        return;

      try
      {
        var client = await HttpClientFactory.CreateClientAsync();

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (!(authState.User.Identity?.IsAuthenticated ?? false))
        {
          Logger.LogWarning("User is not authenticated.");
          return;
        }

        var response = await client.GetAsync("bff/minisplit/status");
        if (response.IsSuccessStatusCode)
        {
          MiniSplitStatus = await response.Content.ReadFromJsonAsync<DeviceStatus>();
          UpdateStatus(MiniSplitStatus);
        }
        else
        {
          var errorBody = await response.Content.ReadAsStringAsync();
          Logger.LogError("Failed to load mini-split status: {StatusCode} - {Body}", response.StatusCode, errorBody);
        }
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Exception occurred while loading mini-split status.");
      }

      IsInitialized = true;
    }

    public async Task TurnOnDevice() =>
        await CallAndUpdateStatusAsync("bff/minisplit/turn-on");

    public async Task TurnOffDevice() =>
        await CallAndUpdateStatusAsync("bff/minisplit/turn-off");

    private async Task CallAndUpdateStatusAsync(string endpoint)
    {
      try
      {
        var client = await HttpClientFactory.CreateClientAsync();
        var response = await client.PostAsync(endpoint, content: null);

        if (response.IsSuccessStatusCode)
        {
          var status = await response.Content.ReadFromJsonAsync<DeviceStatus>();
          UpdateStatus(status);
        }
        else
        {
          var errorBody = await response.Content.ReadAsStringAsync();
          Logger.LogWarning("API call to '{Endpoint}' failed: {Status} - {Body}", endpoint, response.StatusCode, errorBody);
        }
      }
      catch (Exception ex)
      {
        Logger.LogError(ex, "Error calling '{Endpoint}'", endpoint);
      }
    }

    private void UpdateStatus(DeviceStatus? status)
    {
      MiniSplitStatus = status;
      IsMiniSplitRunning = MiniSplitStatus?.Switch ?? false;
      IsInitialized = true;
      StateHasChanged();
    }
  }
}
