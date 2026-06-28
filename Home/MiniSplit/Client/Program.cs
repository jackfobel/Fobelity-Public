using Fobelity.Home.MiniSplit.Client;
using Fobelity.Home.MiniSplit.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;
using System.Net.Http.Headers;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.Services.AddMudServices();


//// crank up logs in the browser console
//builder.Logging.SetMinimumLevel(LogLevel.Trace);
//builder.Logging.AddFilter("Microsoft.AspNetCore.Components", LogLevel.Debug);
//builder.Logging.AddFilter("Microsoft.Extensions.Http", LogLevel.Warning);


builder.Services.AddOptions();
builder.Services.AddAuthorizationCore();
builder.Services.TryAddSingleton<AuthenticationStateProvider, HostAuthenticationStateProvider>();
builder.Services.TryAddSingleton(sp => (HostAuthenticationStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

builder.Services.AddTransient<AuthorizedHandler>();

builder.Services.AddHttpClient("default", client =>
{
  client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
  client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddHttpClient("authorizedClient", client =>
{
  client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
  client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
}).AddHttpMessageHandler<AuthorizedHandler>();

// ✨ Use default client where needed
builder.Services.AddTransient(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("default"));
builder.Services.AddTransient<IAntiforgeryHttpClientFactory, AntiforgeryHttpClientFactory>();


builder.Services.AddSingleton(sp =>
    new AppConfig
    {
      Environment = builder.HostEnvironment.Environment
    });



await builder.Build().RunAsync();
