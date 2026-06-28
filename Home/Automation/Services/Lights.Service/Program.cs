using Fobelity.Home.Automation.Lights.Service;
using Fobelity.Home.Automation.Lights.Service.Core.Abstractions;
using Fobelity.Home.Automation.Lights.Service.Core.Models;
using Fobelity.Home.Automation.Lights.Service.Core.Options;
using Fobelity.Home.Automation.Lights.Service.Core.Services;
using Fobelity.Home.Automation.Lights.Service.Infra.Mqtt;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<LightsOptions>(builder.Configuration.GetSection("Lights"));




builder.Services.AddSingleton<IMqttPublisher, EventGridMqttPublisher>();
builder.Services.AddSingleton<ILightService, LightService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/lights", async (ILightService svc, CancellationToken ct)
  => Results.Ok(await svc.ListAsync(ct))).WithTags("Lights");

app.MapGet("/lights/{id}", async (string id, ILightService svc, CancellationToken ct) =>
{
  var light = await svc.GetAsync(id, ct);
  return light is null ? Results.NotFound() : Results.Ok(light);
}).WithTags("Lights");

app.MapPost("/lights/{id}/set", async (string id, LightSetRequest req, ILightService svc, CancellationToken ct) =>
{
  Console.WriteLine($"...Received set request for light '{id}': {System.Text.Json.JsonSerializer.Serialize(req)}");
  var result = await svc.SetAsync(id, req, ct);
  return result is null ? Results.NotFound() : Results.Ok(result);
}).WithTags("Lights");

app.Run();
