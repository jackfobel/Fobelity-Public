using Fobelity.Home.Automation.Lights.Service.Core.Options;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Fobelity.Home.Automation.Lights.Service.Infra.Mqtt;

public sealed class EventGridMqttPublisher : IMqttPublisher, IAsyncDisposable
{
  private readonly ILogger<EventGridMqttPublisher> _log;
  private readonly MqttOptions _opts;

  private readonly IMqttClient _client;
  private readonly SemaphoreSlim _gate = new(1, 1);

  public EventGridMqttPublisher(IOptions<LightsOptions> options, ILogger<EventGridMqttPublisher> log)
  {
    _log = log;
    _opts = options.Value.Mqtt;

    _client = new MqttFactory().CreateMqttClient();

    _client.DisconnectedAsync += e =>
    {
      _log.LogWarning("MQTT disconnected: {Reason}", e.Reason);
      return Task.CompletedTask;
    };
  }

  public async Task PublishJsonAsync(string topic, object payload, CancellationToken ct)
  {
    await EnsureConnectedAsync(ct);

    var json = JsonSerializer.Serialize(payload);

    var qos = _opts.Qos switch
    {
      0 => MqttQualityOfServiceLevel.AtMostOnce,
      2 => MqttQualityOfServiceLevel.ExactlyOnce,
      _ => MqttQualityOfServiceLevel.AtLeastOnce
    };

    var msg = new MqttApplicationMessageBuilder()
      .WithTopic(topic)
      .WithPayload(json)
      .WithQualityOfServiceLevel(qos)
      .Build();

    await _client.PublishAsync(msg, ct);
    _log.LogInformation("MQTT published: {Topic} {Payload}", topic, json);
  }

  private async Task EnsureConnectedAsync(CancellationToken ct)
  {
    if (_client.IsConnected) return;

    await _gate.WaitAsync(ct);
    try
    {
      if (_client.IsConnected) return;

      if (string.IsNullOrWhiteSpace(_opts.Host))
        throw new InvalidOperationException("Lights:Mqtt:Host is required.");

      if (string.IsNullOrWhiteSpace(_opts.Username))
        throw new InvalidOperationException("Lights:Mqtt:Username (authenticationName) is required.");

      var cert = LoadClientCertificate(_opts.CertPath, _opts.KeyPath);

      var options = new MqttClientOptionsBuilder()
        .WithTcpServer(_opts.Host, _opts.Port)
        .WithClientId(_opts.ClientId)
        .WithCredentials(_opts.Username, string.Empty) // Event Grid: username = authenticationName
        .WithCleanSession(true)
        .WithTlsOptions(tls =>
        {
          tls.WithClientCertificates(new[] { cert });
          tls.WithSslProtocols(SslProtocols.Tls12);

          // OK for bring-up; tighten later.
          tls.WithCertificateValidationHandler(_ => true);
        })
        .Build();

      _log.LogInformation("MQTT connecting to {Host}:{Port} as {User} ({ClientId})",
        _opts.Host, _opts.Port, _opts.Username, _opts.ClientId);

      await _client.ConnectAsync(options, ct);
      _log.LogInformation("MQTT connected.");
    }
    finally
    {
      _gate.Release();
    }
  }

  private static X509Certificate2 LoadClientCertificate(string certPath, string keyPath)
  {
    var pem = X509Certificate2.CreateFromPemFile(certPath, keyPath);
    return new X509Certificate2(pem.Export(X509ContentType.Pfx));
  }

  public async ValueTask DisposeAsync()
  {
    try { if (_client.IsConnected) await _client.DisconnectAsync(); } catch { }
    _client.Dispose();
    _gate.Dispose();
  }
}
