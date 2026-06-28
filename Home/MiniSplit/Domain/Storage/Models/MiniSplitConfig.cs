namespace DomainModels.Storage.Models
{
  using Azure;
  using Azure.Data.Tables;
  using System;
  using System.Text.Json.Serialization;

  public class MiniSplitConfigData
  {
    public MiniSplitConfig MiniSplitConfigCool { get; set; }
    public MiniSplitConfig MiniSplitConfigHeat { get; set; }
  }

  public class MiniSplitConfig : ITableEntity
  {
    public string PartitionKey { get; set; }
    public string RowKey { get; set; }
    public DateTimeOffset? Timestamp { get; set; }

    [JsonIgnore]
    public ETag ETag { get; set; }

    public int threshhold { get; set; }
    public bool enabled { get; set; }

    public double costPerKWh { get; set; }
    public double kWhPerHour { get; set; }

    [JsonIgnore]
    public double CostPerHour => costPerKWh * kWhPerHour;

  }
}
