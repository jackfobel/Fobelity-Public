using DomainModels.Device.Interfaces;
using Newtonsoft.Json.Linq;

namespace DomainModels.Device.Models
{
  public class IoTDeviceStatus : IIoTDeviceStatus
  {
    public List<DeviceStatusResult> result { get; set; }
    public bool success { get; set; }
    public long t { get; set; }
    public string tid { get; set; }

  }

  public class DeviceStatusResult : IDeviceResult
  {
    public string code { get; set; }
    public object value { get; set; }
  }

  public class DeviceStatus : IDeviceStatus
  {
    public bool Switch { get; set; }
    public int TempSet { get; set; }
    public int TempCurrent { get; set; }
    public string Mode { get; set; }
    public int HumidityCurrent { get; set; }
    public string TempUnitConvert { get; set; }
    public int TempCurrentF { get; set; }
    public int TempSetF { get; set; }
  }

  public class Root //: IRoot
  {
    public DeviceStatus Result { get; set; }
    public bool Success { get; set; }
    public long T { get; set; }
    public string Tid { get; set; }
  }

  public static class DeviceStatusParser
  {
    public static DeviceStatus Parse(string deviceStatusJson)
    {
      var jsonObject = JObject.Parse(deviceStatusJson);

      bool isSuccess = jsonObject.Value<bool>("success");
      if (!isSuccess)
      {
        var resultToken = jsonObject["result"];

        // Safely handle null result
        if (resultToken != null && resultToken.Type == JTokenType.Array)
        {
          var resultArrayFail = resultToken.ToObject<List<JObject>>();
          // Continue with resultArray processing
        }
        else
        {
          Console.WriteLine("Result is not an array or is null.");

          throw new Exception("Cannot get device status.");
        }
      }

      var resultArray = jsonObject["result"].ToObject<List<JObject>>();

      var deviceStatus = new DeviceStatus();

      foreach (var item in resultArray)
      {
        switch (item["code"].ToString())
        {
          case "switch":
            var test1 = item["value"];
            deviceStatus.Switch = item["value"].ToObject<bool>();
            break;
          case "temp_set":
            deviceStatus.TempSet = item["value"].ToObject<int>();
            break;
          case "temp_current":
            deviceStatus.TempCurrent = item["value"].ToObject<int>();
            break;
          case "mode":
            deviceStatus.Mode = item["value"].ToString();
            break;
          case "humidity_current":
            deviceStatus.HumidityCurrent = item["value"].ToObject<int>();
            break;
          case "temp_unit_convert":
            deviceStatus.TempUnitConvert = item["value"].ToString();
            break;
          case "temp_current_f":
            deviceStatus.TempCurrentF = item["value"].ToObject<int>();
            break;
          case "temp_set_f":
            deviceStatus.TempSetF = item["value"].ToObject<int>();
            break;
        }
      }

      var deviceRoot = new Root
      {
        Result = deviceStatus,
        Success = jsonObject["success"].ToObject<bool>(),
        T = jsonObject["t"].ToObject<long>(),
        Tid = jsonObject["tid"].ToString()
      };

      return deviceStatus;
    }
  }
}
