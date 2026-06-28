using Fobelity.Home.MiniSplit.Domain.Chat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Chat
{
  public static class ChatMessageParser
  {
    public static ParsedMessage Parse(string message)
    {
      var result = new ParsedMessage();
      var startTag = "[START_TABLE]";
      var endTag = "[END_TABLE]";

      int currentIndex = 0;
      while ((currentIndex = message.IndexOf(startTag, currentIndex)) >= 0)
      {
        int endIndex = message.IndexOf(endTag, currentIndex);
        if (endIndex < 0) break;

        var tableContent = message.Substring(
            currentIndex + startTag.Length,
            endIndex - (currentIndex + startTag.Length)).Trim();

        var rows = new List<KeyValuePair<string, string>>();
        var lines = tableContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
          var parts = line.Split(':', 2);
          if (parts.Length == 2)
            rows.Add(new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()));
        }

        result.Tables.Add(rows);

        // Remove this table from the raw message
        message = message.Remove(currentIndex, (endIndex + endTag.Length) - currentIndex);
      }

      result.RawText = message.Trim();
      return result;
    }

  }

}
