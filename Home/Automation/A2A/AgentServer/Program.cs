using A2A;
using A2A.AspNetCore;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

// ===== A2A aliases =====
using A2AMessage = A2A.AgentMessage;
using A2AMessageRole = A2A.MessageRole;
using A2APart = A2A.Part;
using A2ATextPart = A2A.TextPart;
// ===== Persistent Agents aliases =====
using PaClient = Azure.AI.Agents.Persistent.PersistentAgentsClient;
using PaListSortOrder = Azure.AI.Agents.Persistent.ListSortOrder;
using PaMessageRole = Azure.AI.Agents.Persistent.MessageRole;
using PaRunStatus = Azure.AI.Agents.Persistent.RunStatus;
using PaTextItem = Azure.AI.Agents.Persistent.MessageTextContent;


var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
var cfg = builder.Configuration;

var app = builder.Build();
app.MapDefaultEndpoints();

// ---------- Foundry wiring ----------
string Require(string key) => cfg[key] ?? throw new InvalidOperationException($"{key} not set.");

var projectEndpoint = Require("PROJECT_ENDPOINT");
var researcherId = Require("FOUNDRY_RESEARCHER_AGENT_ID");
var actuatorId = Require("FOUNDRY_ACTUATOR_AGENT_ID");

var projectClient = new AIProjectClient(new Uri(projectEndpoint), new DefaultAzureCredential());
var agentsClient = projectClient.GetPersistentAgentsClient();

// Validate both IDs (throws if not found)
_ = await agentsClient.Administration.GetAgentAsync(researcherId);
_ = await agentsClient.Administration.GetAgentAsync(actuatorId);

// ---------- Conversation state (in-memory) ----------
var conversationTtl = TimeSpan.FromMinutes(cfg.GetValue("CONVERSATION_TTL_MINUTES", 8)); // choose X
var threadCache = new MemoryCache(new MemoryCacheOptions());
var threadLocks = new ConcurrentDictionary<string, SemaphoreSlim>();

var runner = new FoundryConversationRunner(agentsClient, threadCache, threadLocks, conversationTtl);

// ---------- A2A managers ----------
var actuatorMgr = MakeManager(
  title: "Fobelity Actuator (A2A)",
  runAsync: (contextId, text, ct) => runner.RunAsync(agentId: actuatorId, contextId, userText: text, ct));

var researchMgr = MakeManager(
  title: "Fobelity Researcher (A2A)",
  runAsync: (contextId, text, ct) => runner.RunAsync(agentId: researcherId, contextId, userText: text, ct));

// ---------- Endpoints ----------
app.MapGet("/healthz", () => Results.Ok(new { ok = true, time = DateTimeOffset.Now }));

// Actuator at root (card -> /.well-known/agent-card.json)
app.MapA2A(actuatorMgr, "/");

// Optional:
// app.MapA2A(researchMgr, "/researcher");

await app.RunAsync();


// ---------------- helpers ----------------

static TaskManager MakeManager(
  string title,
  Func<string, string, CancellationToken, Task<string>> runAsync)
{
  var mgr = new TaskManager();

  mgr.OnAgentCardQuery = (url, _) => Task.FromResult(new AgentCard
  {
    Name = title,
    Description = "Azure AI Foundry�backed A2A endpoint.",
    Url = url,
    Version = "1.0.0",
    DefaultInputModes = new List<string> { "text" },
    DefaultOutputModes = new List<string> { "text" },
    Capabilities = new AgentCapabilities { Streaming = true }
  });

  mgr.OnMessageReceived = async (p, ct) =>
  {
    var userText = p.Message.Parts.OfType<A2ATextPart>().FirstOrDefault()?.Text ?? "";

    // A2A conversation key. If client doesn't send ContextId reliably, you need to provide your own.
    var contextId = string.IsNullOrWhiteSpace(p.Message.ContextId)
      ? "default"
      : p.Message.ContextId;

    var reply = await runAsync(contextId, userText, ct);

    return new A2AMessage
    {
      Role = A2AMessageRole.Agent,
      MessageId = Guid.NewGuid().ToString(),
      ContextId = p.Message.ContextId,
      Parts = new List<A2APart> { new A2ATextPart { Text = reply } }
    };
  };

  return mgr;
}

file sealed record ThreadSession(string ThreadId, DateTimeOffset LastActivityUtc);

file sealed class FoundryConversationRunner
{
  private readonly PaClient _client;
  private readonly IMemoryCache _cache;
  private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
  private readonly TimeSpan _ttl;

  public FoundryConversationRunner(
    PaClient client,
    IMemoryCache cache,
    ConcurrentDictionary<string, SemaphoreSlim> locks,
    TimeSpan ttl)
  {
    _client = client;
    _cache = cache;
    _locks = locks;
    _ttl = ttl;
  }

  public async Task<string> RunAsync(string agentId, string contextId, string userText, CancellationToken ct)
  {
    // Ensure only one run at a time per context/thread.
    var gate = _locks.GetOrAdd(contextId, _ => new SemaphoreSlim(1, 1));
    await gate.WaitAsync(ct);

    try
    {
      var now = DateTimeOffset.UtcNow;

      // Get or create thread session (sliding expiration)
      if (!_cache.TryGetValue(contextId, out ThreadSession? session) ||
          (now - session.LastActivityUtc) > _ttl)
      {
        var created = await _client.Threads.CreateThreadAsync(cancellationToken: ct);
        session = new ThreadSession(created.Value.Id, now);
      }
      else
      {
        session = session with { LastActivityUtc = now };
      }

      _cache.Set(contextId, session, new MemoryCacheEntryOptions
      {
        SlidingExpiration = _ttl
      });

      // Append message to SAME thread
      await _client.Messages.CreateMessageAsync(
        session.ThreadId,
        PaMessageRole.User,
        userText,
        cancellationToken: ct);


      // Run agent
      var run = (await _client.Runs.CreateRunAsync(
          session.ThreadId,
          agentId,
          cancellationToken: ct)).Value;

      static bool IsActive(PaRunStatus s) =>
          s == PaRunStatus.Queued ||
          s == PaRunStatus.InProgress ||
          s == PaRunStatus.RequiresAction;

      while (IsActive(run.Status))
      {
        await Task.Delay(400, ct);
        run = (await _client.Runs.GetRunAsync(
            session.ThreadId,
            run.Id,
            cancellationToken: ct)).Value;
      }

      // IMPORTANT: return the most recent Agent message, not the first.
      // Descending order -> first Agent message encountered is latest.
      await foreach (var m in _client.Messages.GetMessagesAsync(
        session.ThreadId,
        order: PaListSortOrder.Descending,
        cancellationToken: ct))
      {
        if (m.Role == PaMessageRole.Agent)
        {
          var ti = m.ContentItems.OfType<PaTextItem>().FirstOrDefault();
          if (!string.IsNullOrWhiteSpace(ti?.Text))
            return ti.Text!;
        }
      }

      return "(no reply)";
    }
    finally
    {
      gate.Release();
    }
  }
}
