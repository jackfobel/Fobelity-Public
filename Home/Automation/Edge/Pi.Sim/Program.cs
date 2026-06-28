using Fobelity.Home.Automation.Edge;
using Fobelity.Home.Automation.Edge.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<IGpioSwitch, SimSwitch>();
var app = builder.Build();

app.MapGet("/status", async (IGpioSwitch sw, CancellationToken ct) =>
{
  var on = await sw.IsOnAsync(ct);
  return Results.Ok(new DeviceStatus(on, DateTimeOffset.Now));
});
app.MapPost("/led/on", async (IGpioSwitch sw, CancellationToken ct) => { await sw.SetOnAsync(ct); return Results.NoContent(); });
app.MapPost("/led/off", async (IGpioSwitch sw, CancellationToken ct) => { await sw.SetOffAsync(ct); return Results.NoContent(); });

app.Run();

sealed class SimSwitch : IGpioSwitch
{
  volatile bool _on;
  public Task SetOnAsync(CancellationToken ct = default) { _on = true; return Task.CompletedTask; }
  public Task SetOffAsync(CancellationToken ct = default) { _on = false; return Task.CompletedTask; }
  public Task<bool> IsOnAsync(CancellationToken ct = default) => Task.FromResult(_on);
}
