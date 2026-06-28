using Microsoft.ApplicationInsights.DataContracts;

namespace BackendServices.Helpers
{
  public interface ITelemetryClient
  {
    void TrackEvent(string eventName);
    void TrackDependency(DependencyTelemetry telemetry);
    void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null);


    // Add other methods you need to mock
  }
}
