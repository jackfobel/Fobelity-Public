using Azure.Core;
using Azure.Identity;
using DomainModels.Configuration.Interfaces;
using DomainModels.Weather;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace TemperatureMonitoringService
{
  public class TemperatureMonitoringFunction
  {
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TemperatureMonitoringFunction> _logger;
    private readonly IConfiguration _configuration;
    private readonly IConfigurationService _configurationService;

    public TemperatureMonitoringFunction(
        IHttpClientFactory httpClientFactory,
        ILogger<TemperatureMonitoringFunction> logger,
        IConfiguration configuration,
        IConfigurationService configurationService)
    {
      _httpClientFactory = httpClientFactory;
      _logger = logger;
      _configuration = configuration;
      _configurationService = configurationService;
    }

    [Function("TemperatureMonitoringFunction")]
    //public async Task Run([TimerTrigger("0 */5 * * * *", RunOnStartup = true)] TimerInfo myTimer)
    public async Task Run([TimerTrigger("0 */15 * * * *")] TimerInfo myTimer)
    {
      _logger.LogInformation("Timer triggered - checking container availability");

      var httpClient = _httpClientFactory.CreateClient();

      var isReady = await WaitForContainerApiAsync(httpClient, 5);
      if (!isReady)
      {
        _logger.LogWarning("MiniSplit API not ready. Skipping this cycle.");
        return;
      }

      await ExecuteFunctionLogic(httpClient);
    }

    private async Task ExecuteFunctionLogic(HttpClient httpClient)
    {
      try
      {
        var weatherApiUrl = _configurationService.WeatherServiceUrl;

        if (string.IsNullOrWhiteSpace(weatherApiUrl))
          throw new InvalidOperationException("WeatherServiceUrl is not configured.");

        _logger.LogInformation("Fetching weather data from {Url}", weatherApiUrl);

        var response = await httpClient.GetAsync(weatherApiUrl);
        if (!response.IsSuccessStatusCode)
        {
          _logger.LogError("Failed to fetch temperature from weather API. Status code: {StatusCode}", response.StatusCode);
          return;
        }

        var content = await response.Content.ReadAsStringAsync();
        var weatherData = JsonConvert.DeserializeObject<WeatherModel>(content);

        var baseUrl = _configuration["MiniSplitControlServiceBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
          _logger.LogError("MiniSplitControlServiceBaseUrl is not configured.");
          return;
        }

        var controlServiceUrl = $"{baseUrl.TrimEnd('/')}/api/minisplit/automate";


        var credential1 = new ManagedIdentityCredential();
        var credential2 = new DefaultAzureCredential();

        TokenCredential credential = null;

        // May need this?
        var isDevelopment = Environment.GetEnvironmentVariable("AZURE_FUNCTIONS_ENVIRONMENT") == "Development";


        if (isDevelopment)
          credential2 = new DefaultAzureCredential();
        else
          credential2 = new DefaultAzureCredential();
        //credential1 = new ManagedIdentityCredential();

        //var tokenRequestContext = new TokenRequestContext(new[] { "api://controlsvcapi/.default" });
        var tokenRequestContext = new TokenRequestContext(new[] { "https://sanitized.redacted.com/controlsvcapi/.default" });
        

        //_logger.LogInformation("Requesting token for {Scopes}", string.Join(", ", tokenRequestContext.Scopes));


        AccessToken token = default;
        if (isDevelopment)
          token = await credential2.GetTokenAsync(tokenRequestContext);
        else
        {
          token = await credential1.GetTokenAsync(tokenRequestContext);
        }

        //var token = await credential.GetTokenAsync(tokenRequestContext);
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token.Token);

        //_logger.LogInformation("Token Audience(s): {Aud}", string.Join(", ", jwt.Audiences));
        //_logger.LogInformation("Token Issuer: {Issuer}", jwt.Issuer);
        //_logger.LogInformation("Token Roles: {Roles}", string.Join(", ", jwt.Claims
        //    .Where(c => c.Type == "roles")
        //    .Select(c => c.Value)));



        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var retryPolicy = Policy
          .Handle<HttpRequestException>()
          .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
          .WaitAndRetryAsync(
              retryCount: 5,
              sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
              onRetry: (result, delay, retryCount, _) =>
              {
                _logger.LogWarning("Retry {RetryCount} after {Delay}s due to: {Reason}",
                    retryCount, delay.TotalSeconds,
                    result.Exception?.Message ?? result.Result?.ReasonPhrase);
              });

        var jsonContent = JsonConvert.SerializeObject(weatherData);
        var postContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var controlResponse = await retryPolicy.ExecuteAsync(() => httpClient.PostAsync(controlServiceUrl, postContent));

        if (controlResponse.IsSuccessStatusCode)
        {
          _logger.LogInformation("Mini-split control executed successfully at {Time}", DateTime.UtcNow);
        }
        else
        {
          _logger.LogError("Mini-split control failed. Status: {StatusCode}", controlResponse.StatusCode);
          var responseBody = await controlResponse.Content.ReadAsStringAsync();
          _logger.LogError("Response body: {Body}", responseBody);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Unhandled exception during temperature monitoring.");
      }
    }

    private async Task<bool> WaitForContainerApiAsync(HttpClient client, int maxAttempts = 5)
    {
      string? baseUrl = _configuration["MiniSplitControlServiceBaseUrl"];
      Console.WriteLine($"Base URL: {baseUrl}");

      if (string.IsNullOrWhiteSpace(baseUrl))
      {
        _logger.LogWarning("MiniSplitControlServiceBaseUrl is not configured. Skipping readiness check.");
        return false;
      }

      var healthUrl = $"{baseUrl.TrimEnd('/')}/api/health";
      Console.WriteLine($"Health URL: {healthUrl}");

      for (int i = 0; i < maxAttempts; i++)
      {
        try
        {
          var response = await client.GetAsync(healthUrl);
          if (response.IsSuccessStatusCode)
          {
            Console.WriteLine($"Health check successful on attempt {i + 1}");
            return true;
          }
        }
        catch (Exception ex)
        {
          _logger.LogWarning("Attempt {Attempt}: Failed to reach health endpoint. {Message}", i + 1, ex.Message);
        }

        await Task.Delay(2000);
      }

      return false;
    }
  }
}
