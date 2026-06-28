using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using BackendServices;
using BackendServices.Helpers;
using ComfortRulesEngine.Base;
using ComfortRulesEngine.Rules;
using DomainModels.Command.Interfaces;
using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Email.Interfaces;
using DomainModels.Email.Models;
using DomainModels.Storage.Interfaces;
using DomainModels.Token.Interfaces;
using DomainModels.Weather;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;
using NRules;
using NRules.Fluent;
using NRules.RuleModel;
using Serilog;
using System.Security.Claims;

namespace MiniSplitControlService
{
  public class Program
  {
    public static async Task Main(string[] args)
    {
      Console.WriteLine("\uD83D\uDE80 Starting MiniSplitControlService...");

      var builder = WebApplication.CreateBuilder(args);

      builder.AddServiceDefaults();

      builder.WebHost.ConfigureKestrel(serverOptions =>
      {
        var env = builder.Environment;

        if (env.IsDevelopment())
        {
          Console.WriteLine("Development environment detected. Listening on port 5100 with HTTPS enabled for local development.");
          serverOptions.ListenAnyIP(5101, listenOptions =>
          {
            listenOptions.UseHttps(); // Local only
          });
        }
        else
        {
          Console.WriteLine("Production environment detected. Listening on port 8080 for Azure deployment.");
          serverOptions.ListenAnyIP(8080); // Port Azure expects for HTTP inside container
        }
      });


      builder.Configuration
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddEnvironmentVariables();

      builder.Services.AddHttpClient();
      builder.Services.AddControllers();
      builder.Services.AddEndpointsApiExplorer();

      builder.Services.AddSignalR();



      builder.Services.AddSwaggerGen(c =>
      {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = "MiniSplitControlService", Version = "v1" });
      });

      builder.Services.AddSingleton(sp =>
      {
        var keyVaultUri = new Uri($"https://kv-minisplit.vault.azure.net/");
        return new SecretClient(keyVaultUri, new DefaultAzureCredential());
      });

      builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
      builder.Services.AddSingleton<IEmailService, MailKitService>();
      builder.Services.AddSingleton<IWeatherService, WeatherService>();


      var credential = new DefaultAzureCredential();
      var config = builder.Configuration;

      var loggerFactory = LoggerFactory.Create(lb => lb.AddSerilog());
      var logger = loggerFactory.CreateLogger<ConfigurationService>();

      // Call async method before DI
      var secretValue = await SecretHelper.ResolveSecretAsync(config["clientSecret"], logger);

      // Register with DI
      builder.Services.AddSingleton<IConfigurationService>(sp =>
      {
        return new ConfigurationService(config, logger, secretValue);
      });


      builder.Services.AddSingleton<ICommandService>(sp =>
      {
        var logger = sp.GetRequiredService<ILogger<CommandService>>();
        var config = sp.GetRequiredService<IConfigurationService>();
        return new CommandService("Config/commands.json", logger);
      });

      builder.Services.AddSingleton<ITuyaTokenService, TuyaTokenService>(sp =>
      {
        var configurationService = sp.GetRequiredService<IConfigurationService>();
        var logger = sp.GetRequiredService<ILogger<TuyaTokenService>>();
        return new TuyaTokenService(configurationService, logger);
      });
      builder.Services.AddTransient<TokenMiddleware>();

      builder.Services.AddHttpClient<IDeviceService, DeviceService>((sp, client) =>
      {
        var configurationService = sp.GetRequiredService<IConfigurationService>();
        client.BaseAddress = new Uri(configurationService.TuyaEndpoint);
      }).AddHttpMessageHandler<TokenMiddleware>();

      builder.Services.AddSingleton<ITableStorageService>(sp =>
      {
        var configurationService = sp.GetRequiredService<IConfigurationService>();
        var logger = sp.GetRequiredService<ILogger<TableStorageService>>();
        return new TableStorageService(configurationService.StorageConnString, logger);
      });


      builder.Services.AddSingleton<IRuleRepository>(sp =>
      {
        var activator = new CustomRuleActivator(sp);
        var repository = new RuleRepository(activator);

        repository.Load(x => x.From(typeof(CoolTemperatureRuleOn)));
        repository.Load(x => x.From(typeof(CoolTemperatureRuleOff)));
        repository.Load(x => x.From(typeof(HeatTemperatureRuleOn)));
        repository.Load(x => x.From(typeof(HeatTemperatureRuleOff)));


        var logger = sp.GetRequiredService<ILogger<TableStorageService>>();

        logger.LogInformation("Registered total rules: {Count}", repository.GetRules().Count());
        foreach (var rule in repository.GetRules())
        {
          logger.LogInformation("Rule Loaded: {Rule}", rule.Name);
        }

        return repository;
      });


      builder.Services.AddSingleton<ISessionFactory>(sp =>
      {
        var repository = sp.GetRequiredService<IRuleRepository>();
        return repository.Compile();
      });

      builder.Services.AddSingleton<RuntimeTrackingService>();


      if (builder.Environment.EnvironmentName == "Development")
      {
        Console.WriteLine($"Running in Development mode.");


        builder.Services.AddCors(options =>
        {
          options.AddPolicy("AllowDev", policy =>
          {
            policy
              .WithOrigins(
                "https://localhost:5002",
                "https://localhost:5001",
                "https://localhost:7223",
                "https://localhost:7070" // For function app if needed
              )
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials(); // Required for SignalR
          });
        });

        builder.Services.AddAuthentication(options =>
        {
          options.DefaultAuthenticateScheme = "DevScheme";
          options.DefaultChallengeScheme = "DevScheme";
        })
          .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>("DevScheme", options => { });



        builder.Services.AddAuthorization(options =>
        {
          options.FallbackPolicy = new AuthorizationPolicyBuilder()
              .AddAuthenticationSchemes("DevScheme")
              .RequireAssertion(_ => true) // always succeed
              .Build();
        });




        // production might need JwtBearer 
        builder.Services.ConfigureApplicationCookie(options =>
        {
          options.Events.OnRedirectToLogin = context =>
          {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
              context.Response.StatusCode = StatusCodes.Status401Unauthorized;
              return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
          };
        });


      }
      else
      {

        builder.Services.AddCors(options =>
        {
          options.AddPolicy("AllowClientApp", policy =>
          {
            policy
                .WithOrigins("https://YOUR-MINISPLIT-APP.example.com") // MUST match exactly
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // REQUIRED for SignalR
          });
        });





        // Create a custom policy for Foundry so Tools/Actions can be called.
        builder.Services.AddAuthorization(options =>
        {
          // Replace "0c1415af-4fb8-47e8-9204-c1affaf49bda" with your Foundry SP object ID
          var allowedAppObjectIds = new[]
          {
                "0c1415af-4fb8-47e8-9204-c1affaf49bda", // Foundry system identity
              };

          options.AddPolicy("AllowFoundryOrUserAccess", policy =>
          {
            policy.RequireAssertion(context =>
            {
              var roles = context.User.FindAll("roles").Select(c => c.Value).ToList();
              var oid = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

              return roles.Contains("MiniSplit.UserAccess")
                  || allowedAppObjectIds.Contains(oid);
            });
          });

          options.DefaultPolicy = options.GetPolicy("AllowFoundryOrUserAccess")!;
        });


        // Future....?
        //builder.Services.AddAuthorization(options =>
        //{
        //  var allowedAppObjectIds = new[]
        //  {
        //"0c1415af-4fb8-47e8-9204-c1affaf49bda", // Foundry SP object ID
        //  };

        //        var allowedRoles = new[]
        //        {
        //      "MiniSplit.Controller",
        //      "MiniSplit.Scheduler",
        //      "MiniSplit.UserAccess",
        //      "API.Invoker"
        //  };

        //  options.AddPolicy("AllowFoundryOrUserAccess", policy =>
        //  {
        //    policy.RequireAssertion(context =>
        //    {
        //      var userRoles = context.User.FindAll(ClaimTypes.Role)
        //                                  .Select(c => c.Value)
        //                                  .ToList();

        //      var oid = context.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;

        //      return userRoles.Intersect(allowedRoles).Any()
        //          || (oid != null && allowedAppObjectIds.Contains(oid));
        //    });
        //  });

        //  // Optionally make it the default
        //  // options.DefaultPolicy = options.GetPolicy("AllowFoundryOrUserAccess")!;
        //});




        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
          .AddJwtBearer(options =>
          {
            options.Authority = "https://login.microsoftonline.com/399ec66a-adae-4ccf-a618-a747008b3c29/v2.0";
            options.TokenValidationParameters = new TokenValidationParameters
            {
              ValidateAudience = true,
              ValidAudiences = new[]
              {
                "https://localhost:18037",                // Apsire locally.
                "http://localhost:18037",                 // Aspire locally.
                "api://controlsvcapi",                    // May no longer be used.
                "api://dellatestappapi",
                "https://ai.azure.com",                   // Foundry
                "bae01160-d178-44f4-bd82-6cead4f7c899",
                "api://82ddee0c-8f97-48e0-bd1e-b747f7775c21",   // API Controller
                "https://ai.azure.com/.default",          // Foundry Azure AI
                "https://sanitized.redacted.com/controlsvcapi/.default",
                "https://sanitized.redacted.com/dellatestapp",
                "https://sanitized.redacted.com/dellatestapp/.default",
                "82ddee0c-8f97-48e0-bd1e-b747f7775c21"
              },
              ValidateIssuer = true,
              ValidIssuers = new[]
              {
                "https://login.microsoftonline.com/399ec66a-adae-4ccf-a618-a747008b3c29/",
                "https://sts.windows.net/399ec66a-adae-4ccf-a618-a747008b3c29/"
              },
              ValidateLifetime = true,
              RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
              // RoleClaimType = "roles" // Uncomment if needed for WASM client
            };




            options.Events = new JwtBearerEvents
            {
              OnAuthenticationFailed = context =>
              {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "Authentication failed.");
                return Task.CompletedTask;
              },
              OnForbidden = context =>
              {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Forbidden request for user {User}", context.HttpContext.User?.Identity?.Name);
                return Task.CompletedTask;
              },
              OnChallenge = context =>
              {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Challenge triggered. Error: {Error}, Description: {ErrorDescription}", context.Error, context.ErrorDescription);
                return Task.CompletedTask;
              }
            };
          });



        // Needed for wasm client.
        builder.Services.AddAuthorization(options =>
        {
          // DO NOT REMOVE THESE COMMENTS!
          //options.AddPolicy("MiniSplit.UserAccess", policy =>
          //    policy.RequireRole("MiniSplit.UserAccess"));

          //options.AddPolicy("MiniSplit.Controller", policy =>
          //  policy.RequireRole("MiniSplit.Controller"));

          //options.AddPolicy("MiniSplit.Scheduler", policy =>
          //    policy.RequireRole("MiniSplit.Scheduler"));

          //options.AddPolicy("API.Invoker", policy =>
          //    policy.RequireRole("API.Invoker"));


          //// Require Authenticated user for all API endpoints.
          //options.FallbackPolicy = new AuthorizationPolicyBuilder()
          //  .RequireAuthenticatedUser()
          //  .Build();

          // Allow anonymous access to specific endpoints.
          options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();




        });

      }


      Console.WriteLine($"Hosting Environment from builder: {builder.Environment.EnvironmentName}");
      var app = builder.Build();
      app.MapDefaultEndpoints();


      //When running in Development mode(ASPNETCORE_ENVIRONMENT= Development), it injects a fake identity with the MiniSplit.Controller role.
      //This bypasses Azure AD authentication and allows you to test authorized endpoints locally.
      Console.WriteLine($"Hosting Environment from app: {app.Environment.IsDevelopment()}");
      Console.WriteLine($"Runtime Environment: {app.Environment.EnvironmentName}");

      if (app.Environment.IsDevelopment())
      {
        app.UseSwagger();
        app.UseSwaggerUI();

        app.Use(async (ctx, next) =>
        {
          ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
          {
            new Claim("http://schemas.microsoft.com/ws/2008/06/identity/claims/role", "MiniSplit.Scheduler")
        }, "LocalDevelopment"));
          await next();
        });


        app.UseCors(policy =>
            policy.WithOrigins(
                "https://localhost:5002", // your WASM app origin
                "https://localhost:5001", // We have Aspire in the mix, check ports in AppHost.
                "https://localhost:7223"  // optional
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials() // REQUIRED for SignalR over WebSockets with auth
        );
      }
      else
      {
        app.UseCors("AllowClientApp");
      }


      app.UseForwardedHeaders();

      // Reject static files and other non-API requests with 404
      app.Use(async (context, next) =>
      {
        var path = context.Request.Path;

        if (!path.StartsWithSegments("/api")) // && !path.StartsWithSegments("/minisplitHub"))
        {
          context.Response.StatusCode = 404;
          await context.Response.WriteAsync("Not found.");
          return;
        }

        await next();
      });




      app.UseRouting();


      if (app.Environment.IsDevelopment())
      {
        app.UseCors("AllowDev");
      }
      else
      {
        app.UseCors(policy =>
          policy.WithOrigins("https://YOUR-MINISPLIT-APP.example.com")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
      }



      app.Use(async (context, next) =>
      {
        var user = context.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
          var sub = user.FindFirst("sub")?.Value;
          var oid = user.FindFirst("oid")?.Value;
          var roles = user.FindAll("roles").Select(r => r.Value).ToArray();

          //Log.Logger.Information("------------------- Authenticated request:");
          //Log.Logger.Information(" - Subject (sub): {Sub}", sub);
          //Log.Logger.Information(" - Object ID (oid): {Oid}", oid);
          //Log.Logger.Information(" - Roles: {Roles}", string.Join(", ", roles));
        }
        else
        {
          Log.Logger.Warning("xxxxxxxxxxxxxxxxxx - Unauthenticated request");
        }

        await next();
      });

      app.Use(async (context, next) =>
      {
        //Log.Logger.Information("Origin: {Origin}", context.Request.Headers["Origin"]);
        //Log.Logger.Information("Access-Control-Request-Headers: {Headers}", context.Request.Headers["Access-Control-Request-Headers"]);
        await next();
      });


      app.UseAuthentication();
      app.UseAuthorization();

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
      });

      Console.WriteLine("\u2705 Application is ready and running.");
      await app.RunAsync();

    }
  }
}