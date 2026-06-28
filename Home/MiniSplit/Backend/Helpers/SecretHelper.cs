using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using System.Text.RegularExpressions;

namespace BackendServices.Helpers
{
  public static class SecretHelper
  {

    public static async Task<string> ResolveSecretAsync(string value, ILogger logger)
    {
      if (!value.StartsWith("keyvault://", StringComparison.OrdinalIgnoreCase))
        return value?.Trim();

      var match = Regex.Match(value, @"keyvault://(?<vault>[^/]+)/(?<secret>[^/?]+)");
      if (!match.Success)
        throw new InvalidOperationException("Invalid Key Vault URI format.");

      var vaultName = match.Groups["vault"].Value;
      var secretName = match.Groups["secret"].Value;
      var vaultUrl = $"https://{vaultName}.vault.azure.net/";

      logger.LogInformation("🔑 Resolving secret from vault: {VaultUrl}", vaultUrl);

      var credential = new DefaultAzureCredential();
      var client = new SecretClient(new Uri(vaultUrl), credential);
      var secret = await client.GetSecretAsync(secretName);

      return secret.Value.Value;
    }


  }
}
