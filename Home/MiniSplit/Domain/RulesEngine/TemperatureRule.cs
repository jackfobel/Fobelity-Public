namespace DomainModels.RulesEngine
{
  public class TemperatureRule
  {
    public string Id { get; set; }
    public bool Enabled { get; set; }
    public int Threshhold { get; set; }

    public bool IsEnabled => Enabled;
  }




}
