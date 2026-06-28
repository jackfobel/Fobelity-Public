using Azure.Core;
using Azure.Identity;
using BackendServices;
using DomainModels.Configuration.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;

// Create and configure the host
var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
      // Register HttpClient with enhanced logging for token-based authorization
      services.AddHttpClient("MiniSplitControlClient")
          .ConfigurePrimaryHttpMessageHandler(sp =>
          {
            var logger = sp.GetRequiredService<ILogger<BearerTokenHandler>>();

            // Enhanced logging for token retrieval
            logger.LogInformation("Initializing Managed Identity authentication...");

            var credential = new DefaultAzureCredential();

            var tokenRequestContext = new TokenRequestContext(
              //new[] { "api://controlsvcapi/.default" }
              new[] { "https://sanitized.redacted.com/controlsvcapi/.default" }
            );

            try
            {
              var token = credential.GetTokenAsync(tokenRequestContext).GetAwaiter().GetResult();
              logger.LogInformation($"Token retrieved successfully: {token.Token.Substring(0, 20)}...");

              // Decode token to inspect claims
              var handler = new JwtSecurityTokenHandler();
              var jwt = handler.ReadJwtToken(token.Token);

              //logger.LogInformation($"Issuer: {jwt.Issuer}");
              //logger.LogInformation($"Audience(s): {string.Join(", ", jwt.Audiences)}");
              //logger.LogInformation($"Expires: {jwt.ValidTo}");

              //foreach (var claim in jwt.Claims)
              //{
              //  logger.LogInformation($"Claim: {claim.Type} = {claim.Value}");
              //}
            }
            catch (Exception ex)
            {
              logger.LogError($"Failed to retrieve token: {ex.Message}");
            }

            //return new BearerTokenHandler(credential, "api://controlsvcapi/.default", logger);
            return new BearerTokenHandler(credential, "https://sanitized.redacted.com/controlsvcapi/.default", logger);
            //
          });

      // Register configuration service
      //services.AddSingleton<IConfigurationService, ConfigurationService>();

      services.AddSingleton<IConfigurationService>(sp =>
      {
        Console.WriteLine("Initializing ConfigurationService...");

        IConfiguration config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<ConfigurationService>>();
        
        var clientSecret = config["clientSecret"];
        if (string.IsNullOrWhiteSpace(clientSecret))

          throw new InvalidOperationException("Missing configuration key: 'clientSecret'");

        return new ConfigurationService(config, logger, clientSecret);
      });


    })
    .Build();

host.Run();
