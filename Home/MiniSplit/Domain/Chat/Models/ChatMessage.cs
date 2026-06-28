using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Fobelity.Home.MiniSplit.Domain.Chat.Models
{
  public class ChatMessage
  {
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Message { get; set; } = string.Empty;

    public bool IsBot { get; set; }

    public string Text
    {
      get => Message;
      set => Message = value;
    }

    public bool IsUser
    {
      get => !IsBot;
      set => IsBot = !value;
    }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
  }

}
