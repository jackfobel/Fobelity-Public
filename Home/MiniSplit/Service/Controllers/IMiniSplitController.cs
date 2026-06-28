using DomainModels.Command.Models;
using DomainModels.Device.Models;
using DomainModels.Storage.Models;
using DomainModels.Weather;
using Microsoft.AspNetCore.Mvc;

namespace Fobelity.Home.MiniSplit.Service.Controllers
{
  public interface IMiniSplitController
  {
    Task<IActionResult> Automate(WeatherModel weatherModel);
    Task<IActionResult> GetStatus();
    Task<IActionResult> GetDeviceDetails();
    Task<IActionResult> TurnOnDevice();
    Task<IActionResult> TurnOnDeviceWithStatus();
    Task<IActionResult> TurnOffDeviceWithStatus();
    Task<IActionResult> TurnOffDevice();
    Task<IActionResult> LoadConfigData();
    Task<IActionResult> UpdateConfig(MiniSplitConfigData configData);
    Task<IActionResult> GetCurrentTemperature();
  }
}
