//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

using Avatar.Hubs;
using Avatar.Models;
using Avatar.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddSignalR();

var contentTypes = new FileExtensionContentTypeProvider();
contentTypes.Mappings[".glb"] = "model/gltf-binary";
contentTypes.Mappings[".gltf"] = "model/gltf+json";
contentTypes.Mappings[".bin"] = "application/octet-stream";
contentTypes.Mappings[".ktx2"] = "image/ktx2";
contentTypes.Mappings[".fbx"] = "application/octet-stream"; // or "application/x-autodesk-fbx"

// Config ClientSettings
builder.Services.Configure<ClientSettings>(builder.Configuration.GetSection("ClientSettings"));

builder.Services.AddSingleton<ClientContext>();
builder.Services.AddSingleton<IClientService, ClientService>();

// Register HttpClient + typed clients (do NOT also AddSingleton for these types)
builder.Services.AddHttpClient<IceTokenService>();
builder.Services.AddHttpClient<SpeechTokenService>();

// Register the background service to manage token refresh
builder.Services.AddHostedService<TokenRefreshBackgroundService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

app.MapDefaultEndpoints();

if (!app.Environment.IsDevelopment())
{
  app.UseHttpsRedirection();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
  app.UseExceptionHandler("/Home/Error");
  // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
  app.UseHsts();
}

//app.UseHttpsRedirection();

static bool LooksLikeSsml(string? s)
  => !string.IsNullOrWhiteSpace(s) &&
     s.TrimStart().StartsWith("<speak", StringComparison.OrdinalIgnoreCase);

app.MapPost("/api/tts/say", async (
    SpeakRequest req,
    IHubContext<AvatarHub> hub,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
  var hasText = !string.IsNullOrWhiteSpace(req.Text);
  var hasSsml = !string.IsNullOrWhiteSpace(req.Ssml);

  logger.LogInformation(
      "TTS request received. text={HasText} ssml={HasSsml} voice={Voice} clientId={ClientId} animation={Animation}",
      hasText, hasSsml, req.Voice, req.ClientId, req.Animation);

  if (!hasText && !hasSsml)
    return Results.BadRequest("Provide 'text' or 'ssml'.");

  // include animation in the payload
  var payload = new { text = req.Text, ssml = req.Ssml, voice = req.Voice, animation = req.Animation };

  if (!string.IsNullOrWhiteSpace(req.ClientId))
    await hub.Clients.Group(req.ClientId).SendAsync("speak", payload, ct);
  else
    await hub.Clients.All.SendAsync("speak", payload, ct);

  return Results.Ok(new { ok = true });
});

// + GET /api/tts/status -> is a kiosk connected?
app.MapGet("/api/tts/status", () => Results.Ok(new { clients = AvatarHub.ConnectedCount }));

app.UseStaticFiles(new StaticFileOptions
{
  ContentTypeProvider = contentTypes
});

app.UseRouting();

app.UseAuthorization();

app.MapHub<AvatarHub>("/avatarHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
