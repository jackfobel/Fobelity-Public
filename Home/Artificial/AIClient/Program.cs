using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AI.Client
{
  //https://modelcontextprotocol.io/quickstart/client#basic-client-structure-4
  public class Program
  {
    public static async Task Main(string[] args)
    {
      var builder = Host.CreateApplicationBuilder(args);

      // Logging setup
      builder.Logging.AddConsole();

      // Register your AIClient
      builder.Services.AddSingleton<AIClient>();

      var host = builder.Build();

      var client = host.Services.GetRequiredService<AIClient>();
      await client.RunAsync();
    }
  }
}
