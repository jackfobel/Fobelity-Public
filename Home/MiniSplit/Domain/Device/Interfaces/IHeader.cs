namespace DomainModels.Device.Interfaces
{
  public interface IHeader
  {
    string Key { get; set; }
    List<string> Value { get; set; }
  }
}