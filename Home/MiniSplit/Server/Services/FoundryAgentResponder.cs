using Azure.AI.Agents;
using Azure.AI.Agents.Persistent;
using Azure.AI.Projects;
using Azure.Core;
using Fobelity.Home.MiniSplit.Domain.Chat.Interfaces;
using Fobelity.Home.MiniSplit.Domain.Chat.Models;
using Microsoft.Identity.Client;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
//using OpenAI.Assistants;
using System.Text;
using System.Text.Json;

namespace Fobelity.Home.MiniSplit.Server.Services
{
  public class FoundryAgentResponder : IAIResponder
  {
    private readonly IConfiguration _config;
    private readonly AIProjectClient _projectClient;
    private readonly PersistentAgentsClient _agentsClient;

    

    public FoundryAgentResponder(IConfiguration config)
    {
      _config = config;
      //_parser = parser;

      var endpoint = new Uri(_config["AZURE_FOUNDRY:PROJECT_ENDPOINT"]!);
      var accessToken = GetAccessTokenAsync().Result;
      var tokenCredential = new SimpleTokenCredential(accessToken);

      _projectClient = new AIProjectClient(endpoint, tokenCredential);
      _agentsClient = _projectClient.GetPersistentAgentsClient();
    }

    private async Task<string> GetAccessTokenAsync()
    {
      var clientId = _config["AZURE_FOUNDRY:CLIENT_ID"]!;
      var clientSecret = _config["AZURE_FOUNDRY:CLIENT_SECRET"]!;
      var tenantId = _config["AZURE_FOUNDRY:TENANT_ID"]!;
      var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
      var scopes = new[] { "https://ai.azure.com/.default" };

      var app = ConfidentialClientApplicationBuilder.Create(clientId)
        .WithClientSecret(clientSecret)
        .WithAuthority(new Uri(authority))
        .Build();

      var result = await app.AcquireTokenForClient(scopes).ExecuteAsync();
      return result.AccessToken;
    }

    // Raw method for interacting with the agent via ID + text
    public async Task<string> AskAgentAsync(string agentId, string userMessage)
    {
      var thread = _agentsClient.Threads.CreateThread();
      var message = _agentsClient.Messages.CreateMessage(thread.Value.Id, MessageRole.User, userMessage);
      var run = _agentsClient.Runs.CreateRun(thread.Value.Id, agentId);

      while (run.Value.Status == RunStatus.Queued || run.Value.Status == RunStatus.InProgress)
      {
        await Task.Delay(1000);
        run = _agentsClient.Runs.GetRun(thread.Value.Id, run.Value.Id);
      }

      if (run.Value.Status != RunStatus.Completed)
        throw new Exception($"Foundry agent run failed: {run.Value.LastError?.Message}. The MiniSplit Controller API may not be running in Azure.");

      var messages = _agentsClient.Messages.GetMessages(thread.Value.Id);
      var response = new StringBuilder();

      var lastAssistant = messages.LastOrDefault(m => m.Role == MessageRole.Agent);
      if (lastAssistant != null)
      {
        foreach (var item in lastAssistant.ContentItems)
        {
          if (item is MessageTextContent text)
            response.AppendLine(text.Text);
        }
      }



      return response.ToString().Trim();
    }

    // Used when given a ChatMessage object
    public async Task<ChatMessage> AskAgentAsync(ChatMessage input)
    {
      var agentId = _config["AZURE_FOUNDRY:AGENT_ID"]!;
      var responseText = await AskAgentAsync(agentId, input.Message);

      return new ChatMessage
      {
        //User = "AI",
        Message = responseText,
        IsBot = true,
        IsUser = false,
        Timestamp = DateTime.UtcNow
      };
    }

    public async Task<string> GenerateResponseAsync(string input)
    {
      var agentId = _config["AZURE_FOUNDRY:AGENT_ID"]!;
      return await AskAgentAsync(agentId, input);
    }



  }



}
