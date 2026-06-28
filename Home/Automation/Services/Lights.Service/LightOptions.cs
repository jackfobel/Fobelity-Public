namespace Fobelity.Home.Automation.Lights.Service.Core.Options;

public sealed class LightsOptions
{
  public MqttOptions Mqtt { get; init; } = new();
  public LightRegistryOptions Registry { get; init; } = new();
}

public sealed class MqttOptions
{
  public string Host { get; init; } = "";
  public int Port { get; init; } = 8883;

  // IMPORTANT: Event Grid authentication name
  public string Username { get; init; } = "";

  public string ClientId { get; init; } = "lights-service-1";

  public string CertPath { get; init; } = "";
  public string KeyPath { get; init; } = "";

  public int Qos { get; init; } = 1;
}

public sealed class LightRegistryOptions
{
  public Dictionary<string, LightRegistration> Lights { get; init; } = new();
}

public sealed class LightRegistration
{
  public string Name { get; init; } = "";
  public string Location { get; init; } = "";
  public string TopicRoot { get; init; } = "";
}
