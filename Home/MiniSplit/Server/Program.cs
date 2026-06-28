using DomainModels.Token;
using Fobelity.Home.MiniSplit.Domain.Chat.Interfaces;
using Fobelity.Home.MiniSplit.Domain.Token.Interfaces;
using Fobelity.Home.MiniSplit.Server;
using Fobelity.Home.MiniSplit.Server.Services;
using Fobelity.Home.MiniSplit.Service.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using MudBlazor.Services;
using NetEscapades.AspNetCore.SecurityHeaders.Infrastructure;
using System.Security.Claims;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(serverOptions =>
{
  serverOptions.AddServerHeader = false;
});

var services = builder.Services;
var configuration = builder.Configuration;

services.AddMudServices();

// Security Headers
services.AddSecurityHeaderPolicies()
    .SetPolicySelector((PolicySelectorContext ctx) =>
    {
      return SecurityHeadersDefinitions.GetHeaderPolicyCollection(
          builder.Environment.IsDevelopment(),
          configuration["AzureAd:Instance"]);
    });

services.AddScoped<MsGraphService>();
services.AddScoped<CaeClaimsChallengeService>();

// Antiforgery
services.AddAntiforgery(options =>
{
  options.HeaderName = "X-XSRF-TOKEN";
  options.Cookie.Name = "__Host-X-XSRF-TOKEN";
  options.Cookie.SameSite = SameSiteMode.Strict;
  options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});


// Set downstream scopes from configuration
var scopes = configuration.GetValue<string>("DownstreamApi:Scopes");
string[] initialScopes = scopes!.Split(' ', StringSplitOptions.RemoveEmptyEntries);

// MSAL interactive login + downstream token acquisition
services.AddMicrosoftIdentityWebAppAuthentication(configuration, "AzureAd")
    .EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
    .AddMicrosoftGraph("https://graph.microsoft.com/v1.0", initialScopes)
    .AddInMemoryTokenCaches();
//https://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization?tabs=aspnetcore
//consider implementing distributed token caching (e.g., Redis):


// This is the key for mapping MiniSplit.UserAccess approle to ClaimTypes.Role; along with HostAuthenticationStateProvider.
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
  options.ClaimsIssuer = "https://login.microsoftonline.com";

  options.Events.OnSigningIn = context =>
  {
    if (context.Principal?.Identity is not ClaimsIdentity originalIdentity)
      return Task.CompletedTask;

    var claims = originalIdentity.Claims.ToList();

    // Map "name" to ClaimTypes.Name if not already present
    if (!claims.Any(c => c.Type == ClaimTypes.Name))
    {
      var nameClaim = claims.FirstOrDefault(c => c.Type == "name");
      if (nameClaim != null)
      {
        claims.Add(new Claim(ClaimTypes.Name, nameClaim.Value));
      }
    }

    var newIdentity = new ClaimsIdentity(
        claims,
        originalIdentity.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme,
        ClaimTypes.Name,
        ClaimTypes.Role
    );

    context.Principal = new ClaimsPrincipal(newIdentity);
    Console.WriteLine("Claims remapped with authType: " + newIdentity.AuthenticationType);
    return Task.CompletedTask;
  };


});






services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, options =>
{
  options.ClaimActions.MapUniqueJsonKey("role", "roles");
  options.TokenValidationParameters.RoleClaimType = "roles";
});

services.Configure<MicrosoftIdentityOptions>(options =>
{
  options.TokenValidationParameters.RoleClaimType = "roles";
});

services.AddAuthorization(options =>
{
  options.FallbackPolicy = new AuthorizationPolicyBuilder()
      .RequireAuthenticatedUser()
      .Build();
});

services.AddControllersWithViews(options =>
{
  options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

services.AddRazorPages()
  .AddMvcOptions(options =>
  {
    // No need for additional filters here unless customizing
  })
  .AddMicrosoftIdentityUI(); // Enables /MicrosoftIdentity/Account/* routes

// Authenticated HTTP client (for calling protected APIs from the server)
var baseUrl = builder.Configuration["DownstreamApi:BaseUrl"];

builder.Services.AddHttpClient<IApiClient, ApiClient>(client =>
{
  //client.BaseAddress = new Uri("https://sanitized.redacted.com/controlsvcapi/.default");
  client.BaseAddress = new Uri(baseUrl);
})
.AddHttpMessageHandler<AccessTokenHandler>();

// Use same base URL as ApiClient
builder.Services.AddHttpClient<IApiServer, ApiServer>(client =>
{
  client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<AccessTokenHandler>();

builder.Services.AddSignalR();

Console.WriteLine("Starting PollingService...");
builder.Services.AddHostedService<UnifiedPollingService>();

// Determine the AI provider from configuration.
var aiProvider = builder.Configuration["AI:Provider"] ?? "OpenAI";

if (aiProvider.Equals("Foundry", StringComparison.OrdinalIgnoreCase))
{
  // Azure AI Foundry + Tools/Agents.
  builder.Services.AddSingleton<IAIResponder, FoundryAgentResponder>();
}
else
{
  // Artificial.McpServer + Artificial.McpTools.
  builder.Services.AddSingleton<IAIResponder, OpenAIResponder>();
}

builder.Services.AddResponseCompression(opts =>
{
  opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
      ["application/octet-stream"]);
});


if (builder.Environment.EnvironmentName == "Development")
{
  Console.WriteLine($"Running in Development mode.");

  //builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Debug);
  //builder.Logging.AddFilter("Azure", LogLevel.Debug);

}
else
{
  services.AddCors(options =>
  {
    options.AddPolicy("AllowWasmClient", policy =>
    {
      policy.WithOrigins("https://YOUR-MINISPLIT-APP.example.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR
    });
  });
}


var app = builder.Build();

if (app.Environment.IsDevelopment())
{
  app.UseDeveloperExceptionPage();
  app.UseWebAssemblyDebugging();
}
else
{
  app.UseExceptionHandler("/Error");

  app.UseCors("AllowWasmClient");
}

// Static files w/ .glb mapping
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".glb"] = "model/gltf-binary";
provider.Mappings[".gltf"] = "model/gltf+json";
provider.Mappings[".wav"] = "audio/wav";        // add WAV
provider.Mappings[".wave"] = "audio/wav";        // optional
// (optional extras if you ever use them)
provider.Mappings[".mp3"] = "audio/mpeg";
provider.Mappings[".ogg"] = "audio/ogg";

// If you already have app.UseStaticFiles() earlier, you can keep it,
// and add this second call with the provider to ensure .glb is handled:
app.UseStaticFiles(new StaticFileOptions { ContentTypeProvider = provider });

app.UseResponseCompression();

app.MapHub<UnifiedHub>("/unifiedhub");

app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

//This will prevent your Blazor client from being redirected to login pages when calling secured BFF endpoints and instead return a proper 401 or 403
//app.UseNoUnauthorizedRedirect("/api");
//app.UseNoUnauthorizedRedirect("/"); // fallback if you mix endpoints

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapNotFound("/api/{**segment}");
app.MapFallbackToPage("/_Host");

app.Run();
