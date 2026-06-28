namespace DomainModels.Device.Interfaces
{
  public interface IDeviceService
  {
    Task<T> GetDeviceData<T>(string deviceId, string url);
    Task<T> SendDeviceAction<T>(string deviceId, string url, object jsonPayload);
  }
}