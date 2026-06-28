using Azure;
using Azure.AI.OpenAI;
using Fobelity.Home.MiniSplit.Domain.Chat.Interfaces;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI.Chat;
using System.Text.Json;

namespace Fobelity.Home.MiniSplit.Server.Services
{
  public class OpenAIResponder : IAIResponder
  {
    private readonly IConfiguration _config;
    private readonly IMcpClient _mcpClient;
    private readonly ChatClient _chatClient;
    private readonly ChatCompletionOptions _options;
    private readonly List<ChatMessage> _chatHistory = [];

    public OpenAIResponder(IConfiguration config)
    {
      _config = config;

      string endpoint = config["AZURE_OPENAI:ENDPOINT"]!;
      string apiKey = config["AZURE_OPENAI:API_KEY"]!;
      string model = config["AZURE_OPENAI:MODEL"] ?? "gpt-4o";
      string mcpName = config["MCP_SERVER:NAME"]!;
      string mcpUrl = config["MCP_SERVER:URL"]!;
      string prompt = config["PROMPT"] ?? "You are a facilitator...";

      _options = new ChatCompletionOptions
      {
        Temperature = float.Parse(config["AZURE_OPENAI:Temperature"] ?? "0.7"),
        MaxOutputTokenCount = int.Parse(config["AZURE_OPENAI:MaxOutputTokenCount"] ?? "1600"),
        TopP = float.Parse(config["AZURE_OPENAI:TopP"] ?? "0.95"),
        FrequencyPenalty = float.Parse(config["AZURE_OPENAI:FrequencyPenalty"] ?? "0"),
        PresencePenalty = float.Parse(config["AZURE_OPENAI:PresencePenalty"] ?? "0")
      };

      var transport = new SseClientTransport(new SseClientTransportOptions
      {
        Endpoint = new Uri(mcpUrl),
        Name = mcpName
      });

      _mcpClient = McpClientFactory.CreateAsync(transport).Result;
      foreach (var tool in _mcpClient.ListToolsAsync().Result)
      {
        if (tool.ProtocolTool.InputSchema.ValueKind != JsonValueKind.Object)
          continue;

        var inputSchema = BinaryData.FromString(tool.ProtocolTool.InputSchema.ToString());
        _options.Tools.Add(ChatTool.CreateFunctionTool(tool.Name, tool.Description, inputSchema));
      }

      var openaiClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
      _chatClient = openaiClient.GetChatClient(model);

      _chatHistory.Add(new SystemChatMessage(prompt));
    }

    public Task<Domain.Chat.Models.ChatMessage> AskAgentAsync(Domain.Chat.Models.ChatMessage input)
    {
      throw new NotImplementedException();
    }

    public async Task<string> GenerateResponseAsync(string userMessage)
    {
      _chatHistory.Add(new UserChatMessage(userMessage));
      var completion = await _chatClient.CompleteChatAsync(_chatHistory, _options);
      _chatHistory.Add(new AssistantChatMessage(completion));

      if (completion.Value.FinishReason == ChatFinishReason.ToolCalls)
      {
        foreach (var toolCall in completion.Value.ToolCalls)
        {
          var args = toolCall.FunctionArguments.ToObjectFromJson<Dictionary<string, object>>()!;
          var result = await _mcpClient.CallToolAsync(toolCall.FunctionName, args);
          _chatHistory.Add(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
        }
        return await GenerateResponseAsync("...");
      }

      return completion.Value.Content[0].Text;
    }
  }
}
