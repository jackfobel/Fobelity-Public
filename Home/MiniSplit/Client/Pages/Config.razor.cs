using DomainModels.Storage.Models;
using Fobelity.Home.MiniSplit.Client.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using MudBlazor;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Fobelity.Home.MiniSplit.Client.Pages
{
  public partial class Config
  {
    public bool IsInitialized { get; private set; }
    public MiniSplitConfigData? MiniSplitConfigData { get; set; }

    [Inject] public IAntiforgeryHttpClientFactory AntiforgeryClientFactory { get; set; } = default!;
    [Inject] public ISnackbar Snackbar { get; set; } = default!;
    [Inject] public ILogger<Config> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
      if (IsInitialized)
        return;

      try
      {
        var client = await AntiforgeryClientFactory.CreateClientAsync();
        var response = await client.GetAsync("bff/minisplit/config-data");
        response.EnsureSuccessStatusCode();

        MiniSplitConfigData = await response.Content.ReadFromJsonAsync<MiniSplitConfigData>();
        Console.WriteLine($"Loaded config: Cool.enabled={MiniSplitConfigData?.MiniSplitConfigCool?.enabled}, Heat.enabled={MiniSplitConfigData?.MiniSplitConfigHeat?.enabled}");

        IsInitialized = true;
      }
      catch (Exception ex)
      {
        Snackbar.Add($"Failed to load config: {ex.Message}", Severity.Error);
      }

      StateHasChanged();
    }

    protected async Task HandleClick()
    {
      if (MiniSplitConfigData is null)
        return;

      try
      {
        var json = JsonSerializer.Serialize(MiniSplitConfigData);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var client = await AntiforgeryClientFactory.CreateClientAsync();
        var response = await client.PostAsync("bff/minisplit/update-config", content);

        if (response.IsSuccessStatusCode)
        {
          Snackbar.Add("Saved successfully!", Severity.Success);

          // No Content being returned (204)
          //await response.Content.ReadFromJsonAsync<MiniSplitConfigData>();

          IsInitialized = true;
        }
        else
        {
          Snackbar.Add("Failed to save config.", Severity.Error);
        }
      }
      catch (Exception ex)
      {
        Snackbar.Add($"Error saving config: {ex.Message}", Severity.Error);
      }
    }
  }
}
