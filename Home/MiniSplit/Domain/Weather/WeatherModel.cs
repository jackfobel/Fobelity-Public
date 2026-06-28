namespace DomainModels.Weather
{
  // Model for deserializing weather API response
  public class WeatherModel
  {
    public Coord Coord { get; set; }
    public WeatherData[] Weather { get; set; }
    public string Base { get; set; }
    public MainData Main { get; set; }
    public int Visibility { get; set; }
    public Wind Wind { get; set; }
    public Clouds Clouds { get; set; }
    public long Dt { get; set; }
    public Sys Sys { get; set; }
    public int Timezone { get; set; }
    public int Id { get; set; }
    public string Name { get; set; }
    public int Cod { get; set; }
  }

  public class Coord
  {
    public float Lon { get; set; }
    public float Lat { get; set; }
  }

  public class WeatherData
  {
    public int Id { get; set; }
    public string Main { get; set; }
    public string Description { get; set; }
    public string Icon { get; set; }
  }

  public class MainData
  {
    public float Temp { get; set; }
    public float FeelsLike { get; set; }
    public float TempMin { get; set; }
    public float TempMax { get; set; }
    public int Pressure { get; set; }
    public int Humidity { get; set; }
    public int SeaLevel { get; set; }
    public int GrndLevel { get; set; }
  }

  public class Wind
  {
    public float Speed { get; set; }
    public int Deg { get; set; }
    public float Gust { get; set; }
  }

  public class Clouds
  {
    public int All { get; set; }
  }

  public class Sys
  {
    public int Type { get; set; }
    public string Country { get; set; }
    public long Sunrise { get; set; }
    public long Sunset { get; set; }
  }

  public class WeatherForecast
  {
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
  }
}
