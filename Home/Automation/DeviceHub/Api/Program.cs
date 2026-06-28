using Azure.Identity;
using Fobelity.Home.Automation.Adapters.Seam;
using Fobelity.Home.Automation.Adapters.Tuya;
using Fobelity.Home.Automation.DeviceHub.Api; // RouterInventory
using Fobelity.Home.Automation.DeviceHub.Core.Abstractions;
using Fobelity.Home.Automation.DeviceHub.Core.Models;
using Fobelity.Home.Automation.Lights.Service.Core.Abstractions;
using Fobelity.Home.Automation.Lights.Service.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.OpenApi.Models;
using Seam.Api;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var cfg = builder.Configuration;

// -------- AUTH MODE: Dev | Jwt | None (default: Dev) --------
var authMode = (cfg["AUTH_MODE"] ?? "Dev").Trim().ToLowerInvariant();

if (authMode == "jwt")
{
  // This configuration allows Azure AI Foundry to talk to our container app.
  builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApi(jwtOptions =>
    {
      // Bind the standard AzureAd block (Instance, TenantId, ClientId, etc.)
      builder.Configuration.Bind("AzureAd", jwtOptions);

      // Accept tokens targeted either at GUID clientId or your App ID URI
      var clientId = builder.Configuration["AzureAd:ClientId"];
      var appIdUri = builder.Configuration["AzureAd:Audience"]; // e.g., https://fobelity.com/ca-devicehubapi

      jwtOptions.TokenValidationParameters.ValidAudiences =
        new[] { clientId, appIdUri }.Where(s => !string.IsNullOrWhiteSpace(s));

      // Prefer "roles" as RoleClaimType and "name" as NameClaimType
      jwtOptions.TokenValidationParameters.NameClaimType = "name";
      jwtOptions.TokenValidationParameters.RoleClaimType = "roles";

      // (Optional) Lock to the Foundry workspace Managed Identity appId if provided
      var allowedCallerAppId = builder.Configuration["AZURE_FOUNDRY:WORKSPACE_MI_CLIENT_ID"];
      jwtOptions.Events = new JwtBearerEvents
      {
        OnTokenValidated = ctx =>
        {
          if (!string.IsNullOrWhiteSpace(allowedCallerAppId))
          {
            var caller = ctx.Principal?.FindFirst("appid")?.Value   // application permission
                      ?? ctx.Principal?.FindFirst("azp")?.Value;     // some STS set azp

            if (!string.Equals(caller, allowedCallerAppId, StringComparison.OrdinalIgnoreCase))
            {
              ctx.Fail("Caller application not allowed.");
            }
          }
          return Task.CompletedTask;
        }
      };
    },
    msIdOpts => builder.Configuration.Bind("AzureAd", msIdOpts));

  // Authorization: pass if delegated scope OR any role claim contains the expected value
  builder.Services.AddAuthorization(o =>
  {
    o.AddPolicy("DeviceHub.Access", policy =>
      policy.RequireAssertion(ctx =>
      {
        // delegated (user) tokens
        var scopes = (ctx.User.FindFirst("scp")?.Value ?? "")
          .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var hasScope = scopes.Contains("DeviceHub.Invoke");


        // application tokens (A2A) � handle all common role claim types
        var hasRole = ctx.User.Claims.Any(c =>
            (c.Type == "roles" ||
             c.Type == "role" ||
             c.Type == System.Security.Claims.ClaimTypes.Role)
            && string.Equals(c.Value, "DeviceHub.AccessAsApp", StringComparison.Ordinal));

        return hasScope || hasRole;
      }));

    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
  });

}
else if (authMode == "dev")
{
 
  builder.Services
    .AddAuthentication("Dev")
    .AddScheme<AuthenticationSchemeOptions, DevAllowAllHandler>("Dev", options =>
    {
      options.TimeProvider = TimeProvider.System; // or a custom TimeProvider
    });

  builder.Services.AddAuthorization(o =>
  {
    o.AddPolicy("DeviceHub.Access", p => p.RequireAssertion(_ => true));

    o.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Dev")
        .RequireAssertion(_ => true)
        .Build();

    o.FallbackPolicy = o.DefaultPolicy;
  });



  builder.Services.AddAuthorization(o =>
  {
    o.DefaultPolicy = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Dev")
        .RequireAssertion(_ => true)
        .Build();
    o.FallbackPolicy = o.DefaultPolicy;
  });
}
else // none
{
  builder.Services.AddAuthorization(o =>
  {
    o.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();
    o.FallbackPolicy = o.DefaultPolicy;
  });

  builder.Services.AddAuthorization(o =>
  {
    o.AddPolicy("DeviceHub.Access", p => p.RequireAssertion(_ => true));

    o.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();

    o.FallbackPolicy = o.DefaultPolicy;
  });

}

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
  c.SwaggerDoc("v1", new() { Title = "DeviceHub", Version = "v1" });

  // Bearer auth in Swagger
  var bearer = new OpenApiSecurityScheme
  {
    Name = "Authorization",
    Type = SecuritySchemeType.Http,
    Scheme = "bearer",
    BearerFormat = "JWT",
    In = ParameterLocation.Header,
    Description = "Paste only the access token; 'Bearer' prefix is added automatically.",
    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
  };
  c.AddSecurityDefinition("Bearer", bearer);
  c.AddSecurityRequirement(new OpenApiSecurityRequirement
  {
    [bearer] = Array.Empty<string>()
  });

  c.DocumentFilter<ServersDocumentFilter>();
});

// ---------- MiniSplit outbound auth (to ca-controlsvc) ----------
var msBase = builder.Configuration["MINISPLIT_BASE_URL"]
            ?? "https://YOUR-CONTROL-SERVICE.example.com";

// Scope for client-credential tokens to call the controller API
var msScope = builder.Configuration["MINISPLIT_SCOPE"]
            ?? "https://YOUR-TENANT.onmicrosoft.com/YOUR-API/.default";

var msStatic = builder.Configuration["MINISPLIT_BEARER"]; // optional: paste a test JWT

builder.Services.AddSingleton<IAccessTokenSource>(sp =>
{
  if (!string.IsNullOrWhiteSpace(msStatic)) return new StaticTokenSource(msStatic);

  if (!string.IsNullOrWhiteSpace(msScope))
  {
    // In ACA, this will use the container app's Managed Identity by default
    var cred = new DefaultAzureCredential();
    return new EntraTokenSource(cred, msScope);
  }

  return new StaticTokenSource(null);
});

builder.Services.AddTransient<BearerHandler>();
builder.Services.AddHttpClient<MiniSplitHttpClient>(c => c.BaseAddress = new Uri(msBase))
                .AddHttpMessageHandler<BearerHandler>();

// ---------- Seam (Ecobee) ----------
builder.Services.AddHttpClient<SeamThermostatClient>((sp, c) =>
{
  var cfg = sp.GetRequiredService<IConfiguration>();
  c.BaseAddress = new Uri("https://api.seam.co/");
  c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
  var key = cfg["SEAM_API_KEY"];
  if (!string.IsNullOrWhiteSpace(key))
    c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
});

// The Lights service.
// Use it this way: sp.GetRequiredService<ILightService>()
builder.Services.AddHttpClient<ILightService, LightsServiceClient>(c =>
{
  var baseUrl = builder.Configuration["LIGHTS_BASE_URL"]
    ?? throw new InvalidOperationException("LIGHTS_BASE_URL is required");
  c.BaseAddress = new Uri(baseUrl);
});




// Adapters
builder.Services.AddSingleton<TuyaMiniSplitAdapter>();

// Seam SDK singletons
builder.Services.AddSingleton(sp =>
    new Seam.Client.SeamClient(apiToken: builder.Configuration["SEAM_API_KEY"]));
builder.Services.AddSingleton(sp =>
    new Seam.Api.Thermostats(sp.GetRequiredService<Seam.Client.SeamClient>()));

// Ecobee adapter
builder.Services.AddSingleton<EcobeeThermostatAdapter>(sp =>
{
  var seam = sp.GetRequiredService<Seam.Client.SeamClient>();
  var api = sp.GetRequiredService<Seam.Api.Thermostats>();
  var deviceId = builder.Configuration["ECOBEE_DEVICE_ID"]
                 ?? throw new InvalidOperationException("ECOBEE_DEVICE_ID is required");
  return new EcobeeThermostatAdapter(seam, api, deviceId);
});

// Providers -> Inventory
builder.Services.AddSingleton<IThermostatProvider, TuyaMiniSplitProvider>();
builder.Services.AddSingleton<IThermostatProvider, SeamEcobeeProvider>();
builder.Services.AddSingleton<IDeviceInventory, RouterInventory>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.UseSwagger();
app.UseSwaggerUI();

if (authMode is "jwt" or "dev") app.UseAuthentication();
app.UseAuthorization();

// -------------------- Endpoints --------------------

//// Quick diagnostic (call with a token) � helps verify claims when chasing 401/403s
//// ---- DEBUG: whoami ----
//app.MapGet("/__whoami",
//    [AllowAnonymous] (ClaimsPrincipal user) =>
//    {
//      // When anonymous, user.Identity?.IsAuthenticated will be false
//      var claims = user?.Claims?.Select(c => new { c.Type, c.Value }) ?? Enumerable.Empty<object>();
//      return Results.Ok(new
//      {
//        isAuthenticated = user?.Identity?.IsAuthenticated ?? false,
//        name = user?.Identity?.Name,
//        claims
//      });
//    })
//   .WithTags("Debug")
//   .WithSummary("Show caller identity (anonymous allowed)");

//// Same as above but requires only "authenticated", not the DeviceHub.Invoke policy
//app.MapGet("/__whoami-auth",
//    [Authorize] (ClaimsPrincipal user) =>
//    {
//      var claims = user?.Claims?.Select(c => new { c.Type, c.Value }) ?? Enumerable.Empty<object>();
//      return Results.Ok(new
//      {
//        isAuthenticated = user?.Identity?.IsAuthenticated ?? false,
//        name = user?.Identity?.Name,
//        claims
//      });
//    })
//   .WithTags("Debug")
//   .WithSummary("Show caller identity (auth required, no special policy)");


// /devices
//app.MapGet("/devices",
//    [Authorize(Policy = "DeviceHub.Access")] async (IDeviceInventory inv, CancellationToken ct)
//        => Results.Ok(await inv.ListAsync(ct)))
//   .WithName("listDevices")
//   .WithTags("Devices")
//   .WithSummary("List devices")
//   .WithDescription("Returns device id, name, location, brand, model, and capabilities.")
//   .Produces<IReadOnlyList<Device>>(StatusCodes.Status200OK);

app.MapGet("/devices",
  [Authorize(Policy = "DeviceHub.Access")] async (
    IDeviceInventory inv,
    ILightService lights,
    CancellationToken ct) =>
  {
    // Thermostats (existing inventory)
    var devices = (await inv.ListAsync(ct)).ToList();

    // Lights (from Lights.Service)
    var lightInfos = await lights.ListAsync(ct);

    var lightDevices = lightInfos.Select(l =>
    {
      // Add synonyms into the Name string so the agent can match �bay light�, etc.
      var name = l.Name;
      if (string.Equals(l.Id, "shop-main-bay-ufo-set-01", StringComparison.OrdinalIgnoreCase))
        name = $"{l.Name} (bay light, main bay light, lights)";

      return new Device(
        Id: l.Id,
        Name: name,
        Location: string.IsNullOrWhiteSpace(l.Location) ? "shop" : l.Location,
        Brand: "Zigbee2MQTT",
        Model: "Lights.Service",
        Capabilities: new[] { "light", "switch" }  // keep lower-case for agent matching
      );
    });

    devices.AddRange(lightDevices);

    return Results.Ok(devices);
  })
 .WithName("listDevices")
 .WithTags("Devices")
 .WithSummary("List devices")
 .WithDescription("Returns device id, name, location, brand, model, and capabilities.")
 .Produces<IReadOnlyList<Device>>(StatusCodes.Status200OK);




// /thermostats/{id}/status
app.MapGet("/thermostats/{id}/status",
    [Authorize(Policy = "DeviceHub.Access")] async (string id, IDeviceInventory inv, CancellationToken ct) =>
    {
      var t = await inv.GetThermostatAsync(id, ct);
      if (t is null) return Results.NotFound();
      var status = await t.GetStatusAsync(ct);
      return Results.Ok(status);
    })
   .WithName("thermostatsStatus")
   .WithTags("Thermostats")
   .WithSummary("Get thermostat status")
   .WithDescription("Reads mode, setpoints, temperature, humidity, and fan state.")
   .Produces<ThermostatStatus>(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status404NotFound);

// /thermostats/{id}/set
app.MapPost("/thermostats/{id}/set",
    [Authorize(Policy = "DeviceHub.Access")] async (string id, ThermostatSetRequest req, IDeviceInventory inv, CancellationToken ct) =>
    {
      var t = await inv.GetThermostatAsync(id, ct);
      if (t is null) return Results.NotFound();

      if (req.DryRun)
      {
        var cur = await t.GetStatusAsync(ct);
        return Results.Ok(cur);
      }

      var after = await t.SetAsync(req, ct);
      return Results.Ok(after);
    })
   .WithName("thermostatsSet")
   .WithTags("Thermostats")
   .WithSummary("Set thermostat")
   .WithDescription("Change mode/setpoints; supports dryRun and optional holdMinutes. Shop (Tuya): mode supports on|off only for now.")
   .Accepts<ThermostatSetRequest>("application/json")
   .Produces<ThermostatStatus>(StatusCodes.Status200OK)
   .Produces(StatusCodes.Status404NotFound);

app.MapPost("/lights/{id}/set",
  [Authorize(Policy = "DeviceHub.Access")] async (
    string id,
    LightSetRequest req,
    ILightService lights,
    CancellationToken ct) =>
  {
    Console.WriteLine($"Lights Set called for id={id}");
    var result = await lights.SetAsync(id, req, ct);
    return result is null ? Results.NotFound() : Results.Ok(result);
  })
 .WithName("lightsSet")
 .WithTags("Lights");

app.Run();

// ---- Dev allow-all handler ----
public sealed class DevAllowAllHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
  public DevAllowAllHandler(
      IOptionsMonitor<AuthenticationSchemeOptions> options,
      ILoggerFactory logger,
      UrlEncoder encoder
  ) : base(options, logger, encoder) { }

  protected override Task<AuthenticateResult> HandleAuthenticateAsync()
  {
    var id = new ClaimsIdentity(new[]
    {
      new Claim(ClaimTypes.Name, "dev-user"),
      new Claim(ClaimTypes.Role, "Developer")
    }, Scheme.Name);

    var principal = new ClaimsPrincipal(id);
    var ticket = new AuthenticationTicket(principal, Scheme.Name);
    return Task.FromResult(AuthenticateResult.Success(ticket));
  }
}
