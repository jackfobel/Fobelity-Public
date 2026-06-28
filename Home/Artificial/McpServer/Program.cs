using Fobelity.Home.Artificial.McpTools;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using System.ComponentModel;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole(consoleLogOptions =>
{
  // Configure all logs to go to stderr
  consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<EchoTools>()
    .WithTools<MathTools>()
    .WithTools<TempTools>();

// Add CORS for HTTP transport support in browsers
builder.Services.AddCors(options =>
{
  options.AddDefaultPolicy(policy =>
  {
    policy.AllowAnyOrigin()
          .AllowAnyHeader()
          .AllowAnyMethod();
  });
});

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();



var app = builder.Build();

app.UseCors();
app.MapMcp();
app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


// Add a simple home page
app.MapGet("/status", () => "MCP Server on Azure App Service - Ready for use with HTTP transport");


//app.Run("https://localhost:4001");
app.Run();
