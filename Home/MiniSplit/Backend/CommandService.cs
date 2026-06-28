using DomainModels.Command.Interfaces;
using DomainModels.Command.Models;
using DomainModels.Device.Interfaces;
using Newtonsoft.Json;

namespace BackendServices
{
  public class CommandService : ICommandService
  {
    private readonly ILogger<CommandService> _logger;
    public CommandList CommandList { get; private set; }

    public CommandService(string commandsFilePath, ILogger<CommandService> logger)
    {
      _logger = logger;
      _logger.LogInformation("ℹ️ Initializing CommandService with file: {commandsFilePath}", commandsFilePath);

      CommandList = LoadCommands(commandsFilePath);
    }

    public CommandList LoadCommands(string filePath)
    {
      _logger.LogDebug("ℹ️ Loading commands from {FilePath}", filePath);

      string json = File.ReadAllText(filePath);
      return JsonConvert.DeserializeObject<CommandList>(json);
    }

    public async Task<T> SendCommand<T>(
        string commandName,
        string deviceId,
        IDeviceService deviceService)
    {
      _logger.LogInformation("ℹ️ Sending GET command {CommandName} for device {DeviceId}", commandName, deviceId);


      var command = CommandList.Commands.FirstOrDefault(c => c.Name == commandName);
      if (command == null)
      {
        _logger.LogWarning("❌ Command '{CommandName}' not found.", commandName);

        throw new InvalidOperationException("Command not found.");
      }

      string url = command.Url.Replace("{deviceId}", deviceId);

      var deviceResponse = await deviceService.GetDeviceData<T>(deviceId, url);

      return deviceResponse;
    }

    public async Task<T> SendCommandPost<T>(
        string commandName,
        string deviceId,
        IDeviceService deviceService,
        object jsonPayload)
    {
      _logger.LogInformation("ℹ️ Sending POST command {CommandName} for device {DeviceId}", commandName, deviceId);

      var command = CommandList.Commands.FirstOrDefault(c => c.Name == commandName);
      if (command == null)
      {
        _logger.LogWarning("❌ Command '{CommandName}' not found.", commandName);

        throw new InvalidOperationException("Command not found.");
      }

      string url = command.Url.Replace("{deviceId}", deviceId);
      return await deviceService.SendDeviceAction<T>(deviceId, url, jsonPayload);
    }
  }
}