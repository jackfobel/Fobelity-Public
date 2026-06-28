using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemperatureMonitoringService
{
  // This script scans .cs files for CS8618 warnings and applies basic fixes by:
  // - Marking string properties with '?' if not initialized
  // - OR adding '= string.Empty' default values where applicable
  // - Use cautiously and validate each change

  using System;
  using System.IO;
  using System.Text.RegularExpressions;

  class NullabilityFixer
  {
    static void Main(string[] args)
    {
      string rootDirectory = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
      Console.WriteLine($"Scanning directory: {rootDirectory}");

      foreach (var file in Directory.GetFiles(rootDirectory, "*.cs", SearchOption.AllDirectories))
      {
        string[] lines = File.ReadAllLines(file);
        bool modified = false;

        for (int i = 0; i < lines.Length; i++)
        {
          string line = lines[i].Trim();

          // Match public properties like: public string PropertyName { get; set; }
          var match = Regex.Match(line, @"public (string|int|bool|double|float|DateTime) (\w+) { get; set; }");
          if (match.Success)
          {
            string type = match.Groups[1].Value;
            string name = match.Groups[2].Value;

            if (type == "string")
            {
              lines[i] = lines[i].Replace("string", "string?");
              modified = true;
              Console.WriteLine($"Marked nullable: {name} in {file}");
            }
            else
            {
              lines[i] = lines[i].Replace($"{type} {name} {{ get; set; }}", $"{type} {name} {{ get; set; }} = default;");
              modified = true;
              Console.WriteLine($"Assigned default: {name} in {file}");
            }
          }
        }

        if (modified)
        {
          File.WriteAllLines(file, lines);
          Console.WriteLine($"Updated: {file}\n");
        }
      }
    }
  }

}
