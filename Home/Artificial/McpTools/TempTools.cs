using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.Artificial.McpTools;

/// <summary>
/// This is a simple Temperature Converter tool that provides methods to convert temperatures    
/// between Celsius and Fahrenheit. It includes methods to convert from Celsius to Fahrenheit
/// </summary>
[McpServerToolType]
public sealed class TempTools
{
  [McpServerTool, Description("Converts temperature from Celsius to Fahrenheit.")]
  public static double CelsiusToFahrenheit(double celsius) => (celsius * 9 / 5) + 32;

  [McpServerTool, Description("Converts temperature from Fahrenheit to Celsius.")]
  public static double FahrenheitToCelsius(double fahrenheit) => (fahrenheit - 32) * 5 / 9;
}
