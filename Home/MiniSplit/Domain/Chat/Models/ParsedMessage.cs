using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Chat.Models
{
  public class ParsedMessage
  {
    public string? RawText { get; set; }

    // True if at least one table was detected
    public bool IsTable => Tables.Any();

    // Each inner list is one table
    public List<List<KeyValuePair<string, string>>> Tables { get; set; } = new();
  }
}
