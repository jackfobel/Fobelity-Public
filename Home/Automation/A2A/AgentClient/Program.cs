using A2A;

var baseUrl = new Uri(Environment.GetEnvironmentVariable("A2A_BASE_URL") ?? "https://localhost:5301/");
var card = await new A2ACardResolver(baseUrl).GetAgentCardAsync();

var client = new A2AClient(new Uri(card.Url));

var send = new MessageSendParams
{
  Message = new AgentMessage
  {
    Role = MessageRole.User,
    //Parts = [new TextPart { Text = "hello a2a" }]
    Parts = [new TextPart { Text = "hello a2a, what is the current status of the ecobee ac in the house?" }]
  }
};

var result = await client.SendMessageAsync(send);

if (result is AgentMessage m)
{
  Console.WriteLine(((TextPart)m.Parts[0]).Text);
}
else if (result is AgentTask firstTask)
{
  var final = await WaitForCompletionAsync(client, firstTask);

  var text = final.Artifacts?
      .LastOrDefault()?
      .Parts?
      .OfType<TextPart>()
      .FirstOrDefault()?
      .Text ?? "(no output)";

  Console.WriteLine(text);
}
else
{
  Console.WriteLine("Unexpected response type.");
}

static async Task<AgentTask> WaitForCompletionAsync(A2AClient client, AgentTask task, CancellationToken ct = default)
{
  // Version-agnostic: anything not Completed/Failed is “still working”
  while (task.Status.State != TaskState.Completed && task.Status.State != TaskState.Failed)
  {
    await Task.Delay(750, ct);
    task = await client.GetTaskAsync(task.Id, ct);
    // optional trace:
    // Console.WriteLine($"state: {task.Status.State}");
  }
  return task;
}
