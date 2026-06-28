using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Fobelity.Home.Artificial.McpTools;

/// <summary>
///  This is a simple echo tool that provides various methods to interact with messages.
///  It includes methods to echo a message, flip a message, and return the length of a message.
/// </summary>
[McpServerToolType]
public sealed class EchoTools
{
  [McpServerTool, Description("Echoes the message back to the client.")]
  public static string Echo(string message) => $"hello {message}";

  [McpServerTool, Description("Flip the message back to the client.")]
  public static string Flip(string message) => new string(message.Reverse().ToArray());

  [McpServerTool, Description("Return the message length back to the client.")]
  public static int MLength(string message) => message.Length;
}