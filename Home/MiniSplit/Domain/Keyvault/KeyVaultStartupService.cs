//using Microsoft.Extensions.Configuration.Memory;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Microsoft.Extensions.Hosting;
//using Azure.Security.KeyVault.Secrets;

//namespace DomainModels.Keyvault
//{
//  public class KeyVaultStartupService : IHostedService
//  {
//    private readonly SecretClient _secretClient;
//    private readonly IConfiguration _configuration;
//    private readonly ILogger<KeyVaultStartupService> _logger;

//    public KeyVaultStartupService(SecretClient secretClient, IConfiguration configuration, ILogger<KeyVaultStartupService> logger)
//    {
//      _secretClient = secretClient;
//      _configuration = configuration;
//      _logger = logger;
//    }

//    public async Task StartAsync(CancellationToken cancellationToken)
//    {
//      try
//      {
//        _logger.LogInformation("Starting to obtain smtp-pass from Key Vault.");

//        var smtpSecret = await _secretClient.GetSecretAsync("smtp-pass", cancellationToken: cancellationToken);
//        var value = smtpSecret.Value.Value;

//        _logger.LogInformation("Successfully retrieved smtp-pass from Key Vault.");

//        if (_configuration is IConfigurationRoot root)
//        {
//          var memoryProvider = root.Providers
//              .OfType<MemoryConfigurationProvider>()
//              .FirstOrDefault();

//          if (memoryProvider != null)
//          {
//            memoryProvider.Set("EmailSettings:Password", value);
//            _logger.LogInformation("Successfully injected smtp-pass into configuration.");
//          }
//          else
//          {
//            _logger.LogWarning("No in-memory config provider found to inject smtp-pass.");
//          }
//        }
//      }
//      catch (Exception ex)
//      {
//        _logger.LogError(ex, "Failed to retrieve or inject smtp-pass from Key Vault.");
//      }
//    }


//    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
//  }
//}
