namespace Fobelity.Home.Automation.Lights.Service
{
  public interface IMqttPublisher
  {
    Task PublishJsonAsync(string topic, object payload, CancellationToken ct);
  }
}
