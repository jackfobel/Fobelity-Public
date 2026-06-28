using Fobelity.Home.MiniSplit.Domain.Chat.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Chat.Interfaces
{
  public interface IAIResponder
  {
    Task<string> GenerateResponseAsync(string input);

    Task<ChatMessage> AskAgentAsync(ChatMessage input);
  }

}
