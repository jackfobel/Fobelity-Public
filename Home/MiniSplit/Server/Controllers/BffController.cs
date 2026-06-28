using DomainModels.Device.Models;
using DomainModels.Storage.Models;
using DomainModels.Weather;
using Fobelity.Home.MiniSplit.Domain.Token.Interfaces;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Fobelity.Home.MiniSplit.Server.Controllers
{
  [ApiController]
  [Route("bff/minisplit")]
  [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
  [Authorize(Roles = "MiniSplit.UserAccess")]
  public class BffController : ControllerBase
  {
    private readonly IApiClient _apiClient;
    private readonly ITokenAcquisition _tokenAcquisition;

    public BffController(IApiClient apiClient, ITokenAcquisition tokenAcquisition)
    {
      _apiClient = apiClient;
      _tokenAcquisition = tokenAcquisition;
    }

    private async Task<string> GetAccessTokenAsync()
    {
      var token = await _tokenAcquisition.GetAccessTokenForUserAsync(
        new[] { "https://sanitized.redacted.com/controlsvcapi/.default" });

      ////////Console.WriteLine($"-------------------->>> token: {token}");

      return token;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
      var token = await GetAccessTokenAsync();
      var status = await _apiClient.GetMiniSplitStatusAsync(token);
      return Ok(status);
    }

    [HttpGet("device-details")]
    public async Task<IActionResult> GetDeviceDetails()
    {
      var token = await GetAccessTokenAsync();
      var details = await _apiClient.GetMiniSplitDetailsAsync(token);
      return Ok(details);
    }

    [HttpPost("turn-on")]
    public async Task<IActionResult> TurnOn()
    {
      var token = await GetAccessTokenAsync();
      var result = await _apiClient.TurnOnMiniSplit(token);
      return Ok(result);
    }

    [HttpPost("turn-off")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TurnOff()
    {
      var token = await GetAccessTokenAsync();
      var result = await _apiClient.TurnOffMiniSplit(token);
      return Ok(result);
    }

    [HttpPost("automate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Automate([FromBody] WeatherModel model)
    {
      var token = await GetAccessTokenAsync();
      var result = await _apiClient.AutomateAsync(model, token);
      return Ok(result);
    }

    [HttpGet("config-data")]
    public async Task<IActionResult> GetConfigData()
    {
      var token = await GetAccessTokenAsync();
      var config = await _apiClient.GetMiniSplitConfigDataAsync(token);
      return Ok(config);
    }

    [HttpGet("current-weather")]
    public async Task<IActionResult> GetCurrentWeather()
    {
      var token = await GetAccessTokenAsync();
      var weather = await _apiClient.GetWeatherDataAsync(token);
      return Ok(weather);
    }

    [HttpPost("update-config")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateConfig([FromBody] MiniSplitConfigData config)
    {
      if (!ModelState.IsValid)
      {
        var errors = ModelState
          .Where(e => e.Value?.Errors.Count > 0)
          .Select(e => new
          {
            Field = e.Key,
            Errors = e.Value.Errors.Select(er => er.ErrorMessage)
          });

        return BadRequest(new { Message = "Invalid config", Errors = errors });
      }

      var token = await GetAccessTokenAsync();
      await _apiClient.UpdateMiniSplitConfigAsync(config, token);
      //return Ok(new { success });
      return Ok();
    }
  }
}
