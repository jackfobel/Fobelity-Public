namespace DomainModels.Command.Interfaces
{
  public interface ICommand
  {
    string Description { get; set; }
    string Method { get; set; }
    string Name { get; set; }
    string Url { get; set; }
  }
}