using Fobelity.Home.Automation.Edge.Abstractions;
using Microsoft.Azure.CognitiveServices.Vision.Face;
using Microsoft.Azure.CognitiveServices.Vision.Face.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fobelity.Home.Automation.Edge.Pi.Agent;

public sealed class AzureFaceClient
{
  private readonly IFaceClient _client;
  private readonly string _groupId;
  private readonly double _confidenceMin;

  private sealed class ApiKeyCreds : Microsoft.Rest.ServiceClientCredentials
  {
    private readonly string _key;
    public ApiKeyCreds(string key) => _key = key;
    public override Task ProcessHttpRequestAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
    {
      request.Headers.Add("Ocp-Apim-Subscription-Key", _key);
      return base.ProcessHttpRequestAsync(request, cancellationToken);
    }
  }

  // Note: IConfiguration added so we can read AzureFace:ConfidenceMin even if the options class lacks it
  public AzureFaceClient(IOptions<AzureFaceOptions> opts, IConfiguration cfg)
  {
    var o = opts.Value;
    if (string.IsNullOrWhiteSpace(o.Endpoint)) throw new ArgumentException("AzureFace:Endpoint missing");
    if (string.IsNullOrWhiteSpace(o.Key)) throw new ArgumentException("AzureFace:Key missing");

    _groupId = string.IsNullOrWhiteSpace(o.PersonGroupId) ? "shop" : o.PersonGroupId;

    // Read from config; default to 0.60 if missing/invalid
    _confidenceMin = cfg.GetValue<double?>("AzureFace:ConfidenceMin") ?? 0.60;

    _client = new FaceClient(new ApiKeyCreds(o.Key)) { Endpoint = o.Endpoint };
  }

  /// <summary>Detect faces in an image and return the count.</summary>
  public async Task<int> DetectAsync(string imagePath, CancellationToken ct = default)
  {
    using var fs = File.OpenRead(imagePath);

    var detected = await _client.Face.DetectWithStreamAsync(
        image: fs,
        returnFaceId: true,
        recognitionModel: RecognitionModel.Recognition04,
        detectionModel: DetectionModel.Detection03,
        cancellationToken: ct);

    return detected?.Count ?? 0;
  }

  /// <summary>Identify the most likely known person in the image. Returns (name, confidence) or (null, null).</summary>
  public async Task<(string? name, double? confidence)> IdentifyAsync(string imagePath, CancellationToken ct = default)
  {
    using var fs = File.OpenRead(imagePath);

    var detected = await _client.Face.DetectWithStreamAsync(
        image: fs,
        returnFaceId: true,
        recognitionModel: RecognitionModel.Recognition04,
        detectionModel: DetectionModel.Detection03,
        cancellationToken: ct);

    var faceId = detected.FirstOrDefault()?.FaceId;
    if (faceId == null) return (null, null);

    var identifyResults = await _client.Face.IdentifyAsync(
        faceIds: new[] { faceId.Value },
        personGroupId: _groupId,
        maxNumOfCandidatesReturned: 1,
        confidenceThreshold: _confidenceMin,
        cancellationToken: ct);

    var candidate = identifyResults.FirstOrDefault()?.Candidates?.FirstOrDefault();
    if (candidate == null) return (null, null);

    var person = await _client.PersonGroupPerson.GetAsync(_groupId, candidate.PersonId, ct);
    return (person.Name, candidate.Confidence);
  }

  public async Task<string> CreatePersonAsync(string name, CancellationToken ct = default)
  {
    var p = await _client.PersonGroupPerson.CreateAsync(_groupId, name, userData: null, cancellationToken: ct);
    return p.PersonId.ToString();
  }

  public async Task<string> AddFaceAsync(string personId, string imagePath, CancellationToken ct = default)
  {
    var pid = Guid.Parse(personId);
    using var fs = File.OpenRead(imagePath);
    var pf = await _client.PersonGroupPerson.AddFaceFromStreamAsync(
        _groupId, pid, fs,
        detectionModel: DetectionModel.Detection03,
        userData: null,
        cancellationToken: ct);

    return pf.PersistedFaceId.ToString();
  }

  public Task TrainAsync(CancellationToken ct = default) =>
      _client.PersonGroup.TrainAsync(_groupId, ct);
}
