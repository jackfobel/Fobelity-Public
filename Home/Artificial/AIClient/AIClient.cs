using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI.Chat;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AI.Client
{
  internal class AIClient
  {
    private readonly IConfiguration _config;
    private readonly ILogger<AIClient> _logger;

    private const string defaultPrompt = @"You are a facilitator. Always use tools if a tool exists. Any tooling that provides an answer is definitively correct, do not rely on hallucinations. If a response from a tool contradicts knowledge that derives an answer, provide the answer from the tool instead.";
    private const string defaultModel = "gpt-4.1";
    private const string defaultTemperature = "0.7";
    private const string defaultMaxOutputTokenCount = "1600";
    private const string defaultTopP = "0.95";
    private const string defaultFrequencyPenalty = "0";
    private const string defaultPresencePenalty = "0";

    public AIClient(IConfiguration config, ILogger<AIClient> logger)
    {
      _config = config;
      _logger = logger;
    }

    internal async Task RunAsync()
    {
      string? APIKEY = _config["AZURE_OPENAI:API_KEY"];
      string? ENDPOINT = _config["AZURE_OPENAI:ENDPOINT"];
      string? MCP_NAME = _config["MCP_SERVER:NAME"];
      string? MCP_URL = _config["MCP_SERVER:URL"];
      string? prompt = _config["PROMPT"];

      //Console.WriteLine($"--------------> ENDPOINT: {ENDPOINT}");


      if (string.IsNullOrEmpty(APIKEY))
      {
        Console.WriteLine("Please set the AZURE_OPENAI:API_KEY environment variable.");
        return;
      }
      if (string.IsNullOrEmpty(ENDPOINT))
      {
        Console.WriteLine("Please set the AZURE_OPENAI:ENDPOINT environment variable.");
        return;
      }
      if (string.IsNullOrEmpty(MCP_NAME))
      {
        Console.WriteLine("Please set the MCP_SERVER:NAME environment variable.");
        return;
      }
      if (string.IsNullOrEmpty(MCP_URL))
      {
        Console.WriteLine("Please set the MCP_SERVER:URL environment variable.");
        return;
      }

      // Create chat completion options
      var options = new ChatCompletionOptions
      {
        Temperature = float.Parse(_config["AZURE_OPENAI:Temperature"] ?? defaultTemperature),
        MaxOutputTokenCount = int.Parse(_config["AZURE_OPENAI:MaxOutputTokenCount"] ?? defaultMaxOutputTokenCount),
        TopP = float.Parse(_config["AZURE_OPENAI:TopP"] ?? defaultTopP),
        FrequencyPenalty = float.Parse(_config["AZURE_OPENAI:FrequencyPenalty"] ?? defaultFrequencyPenalty),
        PresencePenalty = float.Parse(_config["AZURE_OPENAI:PresencePenalty"] ?? defaultPresencePenalty)
      };

      IList<ChatTool> chatTools = new List<ChatTool>();
      var mcpTransport = new SseClientTransport(
        new SseClientTransportOptions
        {
          Endpoint = new Uri(MCP_URL),
          Name = MCP_NAME
        }
      );

      //Console.WriteLine("--------------> MCP_URL: " + MCP_URL);
      //Console.WriteLine("--------------> MCP_NAME: " + MCP_NAME);

      IMcpClient mcpClient = null;
      try
      {
        mcpClient = await McpClientFactory.CreateAsync(mcpTransport);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"================> Failed to connect to MCP server: {ex.Message}");
        return;
      }

      IList<McpClientTool> mcpTools = new List<McpClientTool>();
      try
      {
        mcpTools = await mcpClient.ListToolsAsync();
      }
      catch (Exception ex)
      {
        Console.WriteLine($"================> Failed to list tools from MCP server: {ex.Message}");
        return;
      }


      foreach (var tool in mcpTools)
      {
        var inputSchema = tool.ProtocolTool.InputSchema;

        if (inputSchema.ValueKind != JsonValueKind.Object)
        {
          Console.WriteLine($"⚠️ Tool '{tool.Name}' has no usable input schema.");
          continue;
        }

        // Convert JsonElement to BinaryData
        var inputSchemaBinary = BinaryData.FromString(inputSchema.ToString());

        options.Tools.Add(ChatTool.CreateFunctionTool(
            tool.Name,
            tool.Description,
            inputSchemaBinary
        ));
      }




      AzureKeyCredential subscriptionKey = new AzureKeyCredential(APIKEY);
      var azureClient = new AzureOpenAIClient(new Uri(ENDPOINT), subscriptionKey);

      try
      {
        await MessageLoop(azureClient.GetChatClient(_config["AZURE_OPENAI:MODEL"] ?? defaultModel), options, mcpClient, prompt);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"================> An error occurred: {ex.Message}");
      }
    }

    /// <summary>
    /// This is the main message loop for the AI client. It takes input from the user, processes it, and interacts with the chat client and MCP server.
    /// </summary>
    /// <param name="chatClient">The chat client used to communicate with the Azure OpenAI service.</param>
    /// <param name="options">The chat completion options.</param>
    /// <param name="mcpClient">The MCP Client used to communicate with the Model Context Protocol server.</param>
    /// <param name="prompt">The initial prompt for the chat session.</param>
    /// <returns></returns>
    internal async Task MessageLoop(ChatClient chatClient, ChatCompletionOptions options, IMcpClient mcpClient, string prompt)
    {
      string systemPrompt = prompt;
      var chatHistory = new List<ChatMessage>
      {
        new SystemChatMessage(systemPrompt)
      };

      while (true)
      {
        Console.WriteLine("Your prompt:");
        string? userPrompt = Console.ReadLine();
        if (string.IsNullOrEmpty(userPrompt)) break;
        if (userPrompt.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;
        if (userPrompt.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
          Console.WriteLine("Clearing chat history...");
          chatHistory.Clear();
          chatHistory.Add(new SystemChatMessage(prompt));
          continue;
        }
        if (userPrompt.Equals("show prompt", StringComparison.OrdinalIgnoreCase))
        {
          Console.WriteLine("Prompt:");
          Console.WriteLine(systemPrompt);
          continue;
        }
        if (userPrompt.Equals("set prompt", StringComparison.OrdinalIgnoreCase) || userPrompt.Equals("change prompt", StringComparison.OrdinalIgnoreCase))
        {
          Console.WriteLine("Changing prompt:");
          systemPrompt = Console.ReadLine() ?? "";
          if (string.IsNullOrEmpty(systemPrompt))
          {
              Console.WriteLine("Prompt change cancelled.");
              systemPrompt = prompt; 
          }
          chatHistory.Clear();
          chatHistory.Add(new SystemChatMessage(systemPrompt));

          Console.WriteLine("Prompt:");
          Console.WriteLine(systemPrompt);
          continue;
        }

        chatHistory.Add(new UserChatMessage(userPrompt));

        Console.WriteLine("AI Response:");
        chatHistory = await ProcessResponse(chatClient, options, mcpClient, chatHistory);
      }
    }

    /// <summary>
    /// This is the main processing method for the AI client. It takes the chat client, options, MCP client, and chat history,
    /// completes the chat, and processes the response. It handles tool calls and updates the chat history accordingly.
    /// </summary>
    /// <param name="chatClient">The chat client used to communicate with the Azure OpenAI service.</param>
    /// <param name="options">The chat completion options.</param>
    /// <param name="mcpClient">The MCP Client used to communicate with the Model Context Protocol server.</param>
    /// <param name="chatHistory">The chat History of the session.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">We currently only handle Stop and Tool Calls, not other finish reasons</exception>
    internal async Task<List<ChatMessage>> ProcessResponse(ChatClient chatClient, ChatCompletionOptions options, IMcpClient mcpClient, List<ChatMessage> chatHistory)
    {
      ChatCompletion completion = await chatClient.CompleteChatAsync(chatHistory, options);
      chatHistory.Add(new AssistantChatMessage(completion));
      switch (completion.FinishReason)
      {
        case ChatFinishReason.Stop:
          Console.WriteLine(completion.Content[0].Text);
          break;
        case ChatFinishReason.ToolCalls:
          // Handle tool calls => This may be extended later on to handle this in a more asynchronous way vs. the synchronous way here.
          foreach (var toolCall in completion.ToolCalls)
          {
            var mcpArguments = toolCall.FunctionArguments.ToObjectFromJson<Dictionary<string, object>>();
            Console.WriteLine("Tool call detected, calling MCP server...");
            Console.WriteLine($"{toolCall.FunctionName}: {string.Join(", ", (mcpArguments ?? new Dictionary<string, object>()).Select(kvp => $"{kvp.Key}={kvp.Value}"))}");

            var result = await mcpClient.CallToolAsync(toolCall.FunctionName, mcpArguments!);

            switch (result.Content[0].Type)
            {
              case "text":
                Console.WriteLine($"Tool call result {((TextContentBlock)result.Content[0]).Text}");
                chatHistory.Add(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                break;
              default:
                  chatHistory.Add(new ToolChatMessage(toolCall.Id, JsonSerializer.Serialize(result)));
                break;
            }
          }
          chatHistory = await ProcessResponse(chatClient, options, mcpClient, chatHistory);
          break;
        default:
          throw new ArgumentOutOfRangeException();
    }
      return chatHistory;
    }
  }
}