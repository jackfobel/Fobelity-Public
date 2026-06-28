using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace MiniSplitControlService.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  [Authorize(Roles = "MiniSplit.Controller,MiniSplit.Scheduler,MiniSplit.UserAccess,API.Invoker")]
  public class DebugController : ControllerBase
  {
    private readonly IConfiguration _config;
    private readonly ILogger<DebugController> _logger;

    public DebugController(IConfiguration config, ILogger<DebugController> logger)
    {
      _config = config;
      _logger = logger;
    }

    //////[HttpGet("claims")]
    //////public IActionResult Claims()
    //////{
    //////  _logger.LogInformation("DebugController: Claims endpoint hit.");

    //////  var claims = User?.Claims
    //////    .Select(c => new ClaimInfo { Type = c.Type, Value = c.Value })
    //////    .ToList() ?? new List<ClaimInfo>();

    //////  var aud = User.FindFirst("aud")?.Value;
    //////  var iss = User.FindFirst("iss")?.Value;
    //////  Log.Logger.Information(" - Audience (aud): {Aud}", aud);
    //////  Log.Logger.Information(" - Issuer (iss): {Iss}", iss);




    //////  if (User?.Claims != null)
    //////  {
    //////    var oid = User.FindFirst("oid")?.Value;
    //////    _logger.LogInformation($"------------> oid: {oid}");

    //////    foreach (var claim in User.Claims)
    //////    {
    //////      _logger.LogInformation($"Type: {claim.Type}, Value: {claim.Value}");
    //////    }
    //////  }

    //////  //_logger.LogInformation("------------> DebugController: Authenticated: {Authenticated}", User?.Identity?.IsAuthenticated ?? false);
    //////  //_logger.LogInformation("------------> DebugController: Claims: {@Claims}", claims);

    //////  //return Ok(new
    //////  //{
    //////  //  authenticated = User?.Identity?.IsAuthenticated ?? false,
    //////  //  claims
    //////  //});

    //////  return Ok(new
    //////  {
    //////    authenticated = User?.Identity?.IsAuthenticated ?? false,
    //////    oid = User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value,
    //////    appId = User.FindFirst("appid")?.Value,
    //////    roles = User.FindAll("roles").Select(c => c.Value).ToList(),
    //////    claims
    //////  });

    //////}

    [HttpGet("secure")]
    public IActionResult Secure()
    {
      _logger.LogInformation("DebugController: Secure endpoint accessed by authorized user.");
      return Ok("✅ You have the API.Invoker role and reached a secured endpoint.");
    }



    [HttpGet("health")]
    public IActionResult Health()
    {
      _logger.LogInformation("DebugController: Health check endpoint hit.");
      return Ok(new { status = "Healthy", timestamp = DateTime.UtcNow });
    }

    //////[HttpGet("env")]
    //////public IActionResult Env()
    //////{
    //////  _logger.LogInformation("DebugController: Environment variables requested.");

    //////  var envVars = Environment.GetEnvironmentVariables()
    //////    .Cast<DictionaryEntry>()
    //////    .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value?.ToString());

    //////  return Ok(envVars);
    //////}

    //////[HttpGet("config")]
    //////public IActionResult Config()
    //////{
    //////  _logger.LogInformation("DebugController: Config keys requested.");

    //////  var selectedKeys = new[] { "WeatherServiceUrl", "KeyVault:Uri", "AzureWebJobsStorage" };

    //////  var configSnapshot = selectedKeys.ToDictionary(
    //////    key => key,
    //////    key => _config[key] ?? "(not set)"
    //////  );

    //////  return Ok(configSnapshot);
    //////}


  }

  public class ClaimInfo
  {
    public string Type { get; set; }
    public string Value { get; set; }
  }
}
