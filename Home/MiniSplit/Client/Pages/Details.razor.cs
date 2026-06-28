using DomainModels.Device.Models;
using Fobelity.Home.MiniSplit.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;

namespace Fobelity.Home.MiniSplit.Client.Pages
{
  public partial class Details
  {
    public bool IsInitialized { get; private set; } = false;
    public bool IsMiniSplitRunning { get; private set; }
    public DeviceDetails? MiniSplitDetails { get; private set; }
    public DeviceStatus? MiniSplitStatus { get; private set; }

    [Inject] public IAntiforgeryHttpClientFactory HttpClientFactory { get; set; } = default!;
    [Inject] public AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] public ILogger<Details> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
      if (IsInitialized)
        return;

      try
      {
        var client = await HttpClientFactory.CreateClientAsync();
        var response = await client.GetAsync("bff/minisplit/device-details");
        response.EnsureSuccessStatusCode();

        MiniSplitDetails = await response.Content.ReadFromJsonAsync<DeviceDetails>();
        UpdateStatus(MiniSplitStatus); // status might be null until later
      }
      catch (Exception ex)
      {
        Console.WriteLine($"❌ Failed to load mini-split details: {ex.Message}");
      }

      IsInitialized = true;
      StateHasChanged();
    }

    private void UpdateStatus(DeviceStatus? status)
    {
      MiniSplitStatus = status;
      IsMiniSplitRunning = MiniSplitStatus?.Switch ?? false;
    }
  }
}
