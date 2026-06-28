using DomainModels.Command.Interfaces;
using Newtonsoft.Json;

namespace DomainModels.Command.Models
{
  public class Command : ICommand
  {
    public string Name { get; set; }
    public string Url { get; set; }
    public string Method { get; set; }
    public string Description { get; set; }
  }

  public class CommandList
  {
    public List<Command> Commands { get; set; }
  }




}
