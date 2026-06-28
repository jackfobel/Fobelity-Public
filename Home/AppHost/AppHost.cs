using Aspire.Hosting;
using Google.Protobuf.WellKnownTypes;
using System.Net.Sockets;

var builder = DistributedApplication.CreateBuilder(args);

// Service Discovery and Communication.
// https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview?source=recommendations
// https://learn.microsoft.com/en-us/dotnet/core/extensions/service-discovery?tabs=dotnet-cli

//// API End-points.
//var service = builder.AddProject<Projects.Fobelity_Home_MiniSplit_Service>
//  ("fobelity-home-minisplit-service")
//  .WithEndpoint("https", e =>
//  {
//    e.Port = 5100;    // Sets the Aspire port for the service
//    e.TargetPort = 5101;
//    e.Protocol = ProtocolType.Tcp;
//    e.UriScheme = "https";
//  });

//// MCP Server
//var mcpserver = builder.AddProject<Projects.Fobelity_Home_Artificial_McpServer>
//  ("fobelity-home-artificial-mcpserver",
//    launchProfileName: "Fobelity.Home.Artificial.McpServer")
//  .WithEndpoint("https", e =>
//  {

//    e.Port = 4001;
//    e.TargetPort = 4002;
//    e.Protocol = ProtocolType.Tcp;
//    e.UriScheme = "https";
//  });

//// Blazer Server + WASM Client App
//builder.AddProject<Projects.Fobelity_Home_MiniSplit_Server>(
//    "fobelity-home-minisplit-server",
//    launchProfileName: "Fobelity.Home.MiniSplit.Server")
//  .WithReference(service)
//  //.WithReference(mcpserver)
//  .WithEndpoint("https", e =>
//  {

//    e.Port = 5001;
//    e.TargetPort = 5002;
//    e.Protocol = ProtocolType.Tcp;
//    e.UriScheme = "https";
//  });

//
// DeviceHub API and Agent Server are both running in Azure now.
//
//6) Quick test plan
//Run A2A.AgentServer (or launch via Aspire).
//Run A2A.AgentClient -> expect echo: hello a2a.
//Run DeviceHub.Api -> browse /swagger and call /devices (works if you removed/relaxed auth).
//Once stable, we’ll swap the A2A echo for the Foundry thread -> run -> poll code and register DeviceHub as an OpenAPI tool on your Researcher Agent.
//
// Call using the client:
// dotnet run --project .\Home\Automation\A2A\AgentClient\Fobelity.Home.Automation.A2A.AgentClient.csproj
//
// Get the Agent Card - Researcher
// curl -vk https://localhost:5302/.well-known/agent.json
//
// Get the Agent Card - Actuator (Mini-split Control Agent)
// curl -vk https://localhost:5302/actuator/.well-known/agent.json
// 
// 
// DeviceHub API
builder.AddProject<Projects.Fobelity_Home_Automation_DeviceHub_Api>("fobelity-home-automation-devicehub-api")
  .WithEndpoint("https", e =>
  {
    e.Port = 5201;
    e.TargetPort = 5202;
    e.Protocol = ProtocolType.Tcp;
    e.UriScheme = "https";
  });

//// A2A Agent Server
//builder.AddProject<Projects.Fobelity_Home_Automation_A2A_AgentServer>("fobelity-home-automation-a2a-agentserver")
//  .WithEndpoint("https", e =>
//  {
//    e.Port = 5301;
//    e.TargetPort = 5302;
//    e.Protocol = ProtocolType.Tcp;
//    e.UriScheme = "https";
//  });

//builder.AddProject<Projects.Fobelity_Home_Automation_Lights_Service>("fobelity-home-automation-lights-service")
//  .WithEndpoint("https", e =>
//  {
//    e.Port = 5401;
//    e.TargetPort = 5402;
//    e.Protocol = ProtocolType.Tcp;
//    e.UriScheme = "https";
//  });

// Avatar.Server (WebGL + SignalR hub + /api/tts/say)
//var avatar = builder.AddProject<Projects.Avatar>(
//    "fobelity-home-avatar-server")
//  .WithEndpoint("https", e =>
//  {
//    e.Port = 5110;
//    e.TargetPort = 5111;
//    e.Protocol = ProtocolType.Tcp;
//    e.UriScheme = "https";
//  });

builder.Build().Run();
