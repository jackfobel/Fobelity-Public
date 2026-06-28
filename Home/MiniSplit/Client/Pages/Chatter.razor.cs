using Fobelity.Home.MiniSplit.Domain.Chart.Models;
using Fobelity.Home.MiniSplit.Domain.Chat;
using Fobelity.Home.MiniSplit.Domain.Chat.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using MudBlazor;
using System.Globalization;
using System.Security.Claims;

namespace Fobelity.Home.MiniSplit.Client.Pages
{
  public partial class Chatter : IAsyncDisposable
  {
    protected HubConnection? hubConnection;
    protected List<ChatMessage> messages = [];
    protected string? messageInput;
    protected ElementReference messageList;
    private MudTextField<string>? messageInputComponent;
    private ChatMessage? placeholderMessage;
    private System.Timers.Timer? thinkingTimer;
    private int dotCount = 0;

    public string? ToolName { get; private set; }
    public List<DailyChartData> DailySummaryData { get; private set; } = new();
    public Guid? ChartTriggerMessageId { get; private set; }
    public bool ShouldRenderChart { get; private set; }

    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;
    protected ClaimsPrincipal? CurrentUser;

    protected override async Task OnInitializedAsync()
    {
      hubConnection = new HubConnectionBuilder()
        .WithUrl(Navigation.ToAbsoluteUri("/unifiedhub"))
        .WithAutomaticReconnect()
        .Build();

      hubConnection.On<ChatMessage>("ChatterReceiveMessage", async (chatMessage) =>
      {
        if (chatMessage.IsBot && placeholderMessage is not null)
        {
          thinkingTimer?.Stop();
          thinkingTimer?.Dispose();
          thinkingTimer = null;

          var index = messages.IndexOf(placeholderMessage);
          if (index >= 0) messages[index] = chatMessage;
          else messages.Add(chatMessage);

          placeholderMessage = null;
        }
        else
        {
          messages.Add(chatMessage);
        }

        await TryHandleEmbeddedApiResponse(chatMessage);
        await InvokeAsync(StateHasChanged);
        await ScrollToBottom();
      });

      var authState = await AuthStateProvider.GetAuthenticationStateAsync();
      CurrentUser = authState.User;
      await hubConnection.StartAsync();
    }

    private async Task ScrollToBottom() =>
      await JS.InvokeVoidAsync("scrollToBottom", messageList);

    private async Task Send()
    {
      if (hubConnection is null || string.IsNullOrWhiteSpace(messageInput)) return;

      var userMessage = new ChatMessage
      {
        Message = messageInput!,
        IsBot = false,
        Timestamp = DateTime.UtcNow
      };

      await hubConnection.SendAsync("SendChatterMessage", userMessage);
      messageInput = string.Empty;
      await FocusMessageInputAsync();

      placeholderMessage = new ChatMessage
      {
        Message = "🧠 Thinking...",
        IsBot = true,
        Timestamp = DateTime.UtcNow
      };
      messages.Add(placeholderMessage);

      dotCount = 0;
      thinkingTimer = new System.Timers.Timer(500);
      thinkingTimer.Elapsed += (s, e) =>
      {
        dotCount = (dotCount + 1) % 4;
        if (placeholderMessage is not null)
        {
          placeholderMessage.Message = "🧠 Thinking" + new string('.', dotCount);
          InvokeAsync(StateHasChanged);
        }
      };
      thinkingTimer.AutoReset = true;
      thinkingTimer.Start();
    }

    public bool IsConnected => hubConnection?.State == HubConnectionState.Connected;

    public async ValueTask DisposeAsync()
    {
      if (hubConnection is not null) await hubConnection.DisposeAsync();
    }

    private string GetCssClass(ChatMessage msg) => msg.IsBot ? "bot-message" : "user-message";

    private async Task HandleKeyDown(KeyboardEventArgs args)
    {
      if (args.Key == "Enter" && IsConnected &&
          !string.IsNullOrWhiteSpace(messageInput) &&
          !args.ShiftKey)
      {
        await Send();
      }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
      if (firstRender) await FocusMessageInputAsync();
    }

    private async Task FocusMessageInputAsync()
    {
      if (messageInputComponent is not null) await messageInputComponent.FocusAsync();
    }

    private async Task TryHandleEmbeddedApiResponse(ChatMessage msg)
    {
      try
      {
        var parsed = ChatMessageParser.Parse(msg.Message);

        // Preferred: structured K/V tables
        if (parsed.IsTable && parsed.Tables?.Any() == true)
        {
          var list = BuildDailyChartDataFromTables(parsed.Tables);
          if (list.Count > 0)
          {
            ToolName = "summary-daily-breakdown";
            DailySummaryData = list.OrderBy(x => x.Date).ToList();
            ChartTriggerMessageId = msg.Id;
            ShouldRenderChart = true;
            await InvokeAsync(StateHasChanged);
          }
          return;
        }

        // Fallback: plain text block with [START_TABLE]
        if (msg.Message.Contains("[START_TABLE]", StringComparison.OrdinalIgnoreCase))
        {
          ToolName = "summary-daily-breakdown";
          var parsedList = ParsePlainTextTable(msg.Message);
          if (parsedList?.Any() == true)
          {
            DailySummaryData = parsedList.OrderBy(x => x.Date).ToList();
            ChartTriggerMessageId = msg.Id;
            ShouldRenderChart = true;
            await InvokeAsync(StateHasChanged);
          }
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine($"🛑 Failed to process Foundry message: {ex.Message}");
      }
    }

    // ===== Parsing helpers ===================================================

    private static List<DailyChartData> BuildDailyChartDataFromTables(
  List<List<KeyValuePair<string, string>>> tables)
    {
      var result = new List<DailyChartData>();

      foreach (var table in tables)
      {
        DateTime? currentDate = null;
        double? avgTemp = null;
        double? totalCost = null;
        double? totalKWh = null;
        double? thresholdCool = null;
        double? maxOutside = null;
        double? maxInside = null;

        void FlushIfAny()
        {
          if (currentDate.HasValue)
          {
            result.Add(new DailyChartData
            {
              Date = currentDate.Value,
              AvgOutsideTempF = avgTemp,
              TotalCostUSD = totalCost,
              TotalKWhUsed = totalKWh,
              ThresholdCool = thresholdCool,
              MaxOutsideTempF = maxOutside,
              MaxInsideTempF = maxInside
            });
          }
          // reset for next record
          currentDate = null;
          avgTemp = null;
          totalCost = null;
          totalKWh = null;
          thresholdCool = null;
          maxOutside = null;
          maxInside = null;
        }

        foreach (var kv in table)
        {
          var key = NormalizeKey(kv.Key);
          var val = (kv.Value ?? string.Empty).Trim();

          if (key == "date")
          {
            FlushIfAny();
            if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
              currentDate = d;
            continue;
          }

          // avg temp (various wordings)
          if (key.StartsWith("average outside temp"))
          {
            avgTemp ??= TryParseDouble(val);
            continue;
          }

          // total cost
          if (key.StartsWith("total cost") || key.Contains("cost usd"))
          {
            totalCost ??= TryParseDouble(val);
            continue;
          }

          // kWh used
          if (key.Contains("kwh"))
          {
            totalKWh ??= TryParseDouble(val);
            continue;
          }

          // NEW: threshold & max temps (handle wording variants)
          if (key.Contains("threshold") && key.Contains("cool"))
          {
            thresholdCool ??= TryParseDouble(val);
            continue;
          }

          if ((key.Contains("max") || key.Contains("maximum")) && key.Contains("outside") && key.Contains("temp"))
          {
            maxOutside ??= TryParseDouble(val);
            continue;
          }

          if ((key.Contains("max") || key.Contains("maximum")) && key.Contains("inside") && key.Contains("temp"))
          {
            maxInside ??= TryParseDouble(val);
            continue;
          }
        }

        FlushIfAny();
      }

      return result;
    }


    private static string NormalizeKey(string key) =>
      (key ?? string.Empty)
        .Trim()
        .ToLowerInvariant()
        .Replace("°", "")
        .Replace("(f)", "")
        .Replace("(", "")
        .Replace(")", "")
        .Replace(".", "")
        .Replace("temperature", "temp")
        .Replace("  ", " ");

    private static double? TryParseDouble(string? s)
    {
      if (string.IsNullOrWhiteSpace(s)) return null;

      var cleaned = s
        .Replace("$", "")
        .Replace("usd", "", StringComparison.OrdinalIgnoreCase)
        .Replace("°f", "", StringComparison.OrdinalIgnoreCase)
        .Replace("°", "", StringComparison.OrdinalIgnoreCase)
        .Replace("(f)", "", StringComparison.OrdinalIgnoreCase)
        .Replace("kwh", "", StringComparison.OrdinalIgnoreCase)
        .Replace(",", "")    // in case of thousands separators
        .Trim();

      return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)
        ? v
        : (double?)null;
    }

    private List<DailyChartData> ParsePlainTextTable(string message)
    {
      var result = new List<DailyChartData>();
      var lines = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);

      DateTime? currentDate = null;
      double? avgTemp = null, totalCost = null, totalKWh = null;
      double? thresholdCool = null, maxOutside = null, maxInside = null;

      void Flush()
      {
        if (!currentDate.HasValue) return;
        result.Add(new DailyChartData
        {
          Date = currentDate.Value,
          AvgOutsideTempF = avgTemp,
          TotalCostUSD = totalCost,
          TotalKWhUsed = totalKWh,
          ThresholdCool = thresholdCool,
          MaxOutsideTempF = maxOutside,
          MaxInsideTempF = maxInside
        });
        avgTemp = totalCost = totalKWh = thresholdCool = maxOutside = maxInside = null;
      }

      foreach (var raw in lines)
      {
        var line = raw.Trim();
        if (line.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
        {
          Flush();
          var datePart = line.Split(':', 2)[1].Trim();
          if (DateTime.TryParse(datePart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            currentDate = d;
          continue;
        }

        if (line.StartsWith("Average Outside Temp", StringComparison.OrdinalIgnoreCase))
          avgTemp = TryParseDouble(line.Split(':', 2)[1]);

        else if (line.StartsWith("Total Cost", StringComparison.OrdinalIgnoreCase))
          totalCost = TryParseDouble(line.Split(':', 2)[1]);

        else if (line.Contains("kWh", StringComparison.OrdinalIgnoreCase))
          totalKWh = TryParseDouble(line.Split(':', 2)[1]);

        else if (line.StartsWith("Threshold Cool", StringComparison.OrdinalIgnoreCase))
          thresholdCool = TryParseDouble(line.Split(':', 2)[1]);

        else if (line.StartsWith("Max Outside Temp", StringComparison.OrdinalIgnoreCase))
          maxOutside = TryParseDouble(line.Split(':', 2)[1]);

        else if (line.StartsWith("Max Inside Temp", StringComparison.OrdinalIgnoreCase))
          maxInside = TryParseDouble(line.Split(':', 2)[1]);
      }

      Flush();
      return result;
    }

  }
}
