using DomainModels.Command.Models;
using DomainModels.Device.Interfaces;

namespace DomainModels.Command.Interfaces
{
  public interface ICommandService
  {
    CommandList CommandList { get; }

    CommandList LoadCommands(string filePath);
    Task<T> SendCommand<T>(string commandName, string deviceId, IDeviceService deviceService);
    Task<T> SendCommandPost<T>(string commandName, string deviceId, IDeviceService deviceService, object jsonPayload);
  }
}