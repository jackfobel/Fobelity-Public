using DomainModels.Command.Interfaces;
using DomainModels.Configuration.Interfaces;
using DomainModels.Device.Interfaces;
using DomainModels.Device.Models;
using DomainModels.Storage.Interfaces;
using Newtonsoft.Json;
using System.Diagnostics.Eventing.Reader;

namespace BackendServices
{
  public class RuntimeTrackingService
  {
    private readonly IDeviceService _deviceService;
    private readonly ITableStorageService _tableService;
    private readonly ICommandService _commandService;
    private readonly IConfigurationService _configurationService;

    private readonly ILogger<RuntimeTrackingService> _logger;

    public RuntimeTrackingService(
      IDeviceService deviceService,
      ITableStorageService tableService,
      ICommandService commandService,
      IConfigurationService configurationService,
      ILogger<RuntimeTrackingService> logger)
    {
      _deviceService = deviceService;
      _tableService = tableService;
      _commandService = commandService;
      _configurationService = configurationService;
      _logger = logger;

      _logger.LogInformation("ℹ️ Initializing RuntimeTrackingService");
    }

    public async Task TrackMiniSplitRuntimeAsync(string miniSplitId)
    {
      _logger.LogInformation("ℹ️ Calling _commandService.SendCommand");

      var iotDeviceStatus = await _commandService.SendCommand<IoTDeviceStatus>(
          "GetDeviceStatus",
          _configurationService.IoTDeviceId,
          _deviceService
      );

      _logger.LogInformation("ℹ️ iotDeviceStatus set, SerializeObject and parsing");

      string iotDeviceStatusJson = JsonConvert.SerializeObject(iotDeviceStatus);
      var status = DeviceStatusParser.Parse(iotDeviceStatusJson);

      _logger.LogInformation("ℹ️ Getting state from Entity minisplitruntime, minisplit, CurrentState");

      var state = await _tableService.GetEntityAsync<MiniSplitRuntimeState>("minisplitruntime", "minisplit", "CurrentState");

      var now = DateTimeOffset.UtcNow;

      if (state == null)
      {
        // First-time initialization
        var newState = new MiniSplitRuntimeState
        {
          PartitionKey = "minisplit",
          RowKey = "CurrentState",
          IsOn = status.Switch,
          LastChanged = now
        };

        _logger.LogInformation("ℹ️ About to Upsert minisplitruntime");

        await _tableService.UpsertEntityAsync("minisplitruntime", newState);

        _logger.LogInformation("ℹ️ Upsert complete");

        return;
      }

      if (status.Switch != state.IsOn)
      {
        _logger.LogInformation("ℹ️ status.Switch != state.IsOn");

        if (!status.Switch && state.IsOn)
        {
          _logger.LogInformation("ℹ️ !status.Switch && state.IsOn");

          // Mini-split is turning OFF → track ON duration
          var log = new MiniSplitRuntimeLog
          {
            PartitionKey = miniSplitId,
            RowKey = Guid.NewGuid().ToString(),
            StartTime = state.LastChanged,
            EndTime = now,
            Mode = status.Mode,
            Notes = "Mini-split turned off."
          };

          _logger.LogInformation("ℹ️ status.Switch.. hmmm?");

          await _tableService.UpsertEntityAsync("minisplitruntime", log);
        }
        else
        {
          _logger.LogInformation("NOT - ℹ️ !status.Switch && state.IsOn");

        }

        // Update current state to reflect latest switch position
        state.IsOn = status.Switch;
        state.LastChanged = now;

        _logger.LogInformation("ℹ️ About to Upsert LastChanged");

        await _tableService.UpsertEntityAsync("minisplitruntime", state);

        _logger.LogInformation("ℹ️ LastChanged Upserted");
      }
    else
    {
      _logger.LogInformation("ℹ️ NOT - status.Switch != state.IsOn");
    }
    
    }
  }
}
