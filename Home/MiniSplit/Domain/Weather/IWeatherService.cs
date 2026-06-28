using DomainModels.Weather;

namespace DomainModels.Weather
{
  public interface IWeatherService
  {
    Task<WeatherModel> GetCurrentTemperature();
  }
}