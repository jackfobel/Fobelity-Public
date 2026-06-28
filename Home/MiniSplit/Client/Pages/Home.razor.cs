using Microsoft.AspNetCore.Components;

namespace Fobelity.Home.MiniSplit.Client.Pages
{
  public partial class Home
  {
    public bool IsInitialized { get; private set; }

    [Inject] public ILogger<Home> Logger { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
      Console.WriteLine("Home: OnInitializedAsync Fired!");
    }


  }
}