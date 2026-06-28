using DomainModels.Device.Models;

namespace DomainModels.Device.Interfaces
{
  public interface IIoTDeviceStatus
  {
    List<DeviceStatusResult> result { get; set; }
    bool success { get; set; }
    long t { get; set; }
    string tid { get; set; }
  }

  public interface IDeviceResult
  {
    string code { get; set; }
    object value { get; set; }
  }

  public interface IDeviceStatus
  {
    bool Switch { get; set; }
    int TempSet { get; set; }
    int TempCurrent { get; set; }
    string Mode { get; set; }
    int HumidityCurrent { get; set; }
    string TempUnitConvert { get; set; }
    int TempCurrentF { get; set; }
    int TempSetF { get; set; }
  }

  public interface IRoot
  {
    IDeviceStatus Result { get; set; }
    bool Success { get; set; }
    long T { get; set; }
    string Tid { get; set; }
  }


}
