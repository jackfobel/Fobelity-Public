using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.IdentityModel.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackendServices.Helpers
{
  public class TelemetryClientWrapper : ITelemetryClient
  {
    private readonly TelemetryClient _telemetryClient;

    public TelemetryClientWrapper(TelemetryClient telemetryClient)
    {
      _telemetryClient = telemetryClient;
    }

    public void TrackEvent(string eventName)
    {
      _telemetryClient.TrackEvent(eventName);
    }

    public void TrackDependency(DependencyTelemetry telemetry)
    {
      _telemetryClient.TrackDependency(telemetry);
    }

    public void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
    {
      _telemetryClient.TrackEvent(eventName, properties);
    }

    // Implement other methods
  }

}
