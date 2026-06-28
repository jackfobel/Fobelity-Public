namespace DomainModels.Device.Models
{
  public class DeviceDetails
  {
    public DeviceDetailsResult result { get; set; }
    public bool success { get; set; }
    public long t { get; set; }
    public string tid { get; set; }
  }

  public class DeviceDetailsResult
  {
    public long ActiveTime { get; set; }
    public string BindSpaceId { get; set; }
    public string Category { get; set; }
    public long CreateTime { get; set; }
    public string CustomName { get; set; }
    public string Icon { get; set; }
    public string Id { get; set; }
    public string Ip { get; set; }
    public bool IsOnline { get; set; }
    public string Lat { get; set; }
    public string LocalKey { get; set; }
    public string Lon { get; set; }
    public string Model { get; set; }
    public string Name { get; set; }
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public bool Sub { get; set; }
    public string TimeZone { get; set; }
    public long UpdateTime { get; set; }
    public string Uuid { get; set; }
  }
}
