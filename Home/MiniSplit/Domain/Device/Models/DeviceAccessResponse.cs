using DomainModels.Device.Interfaces;

namespace DomainModels.Device.Models
{

  public class Header : IHeader
  {
    public string Key { get; set; }
    public List<string> Value { get; set; }
  }

  public class Content
  {
    public List<Header> Headers { get; set; }
  }

  public class RequestMethod
  {
    public string Method { get; set; }
  }

  public class RequestMessage
  {
    public string Version { get; set; }
    public int VersionPolicy { get; set; }
    public Content Content { get; set; }
    public RequestMethod Method { get; set; }
    public string RequestUri { get; set; }
    public List<Header> Headers { get; set; }
    public Dictionary<string, object> Properties { get; set; }
    public Dictionary<string, object> Options { get; set; }
  }

  public class DeviceAccessResponse
  {
    public string Version { get; set; }
    public Content Content { get; set; }
    public int StatusCode { get; set; }
    public string ReasonPhrase { get; set; }
    public List<Header> Headers { get; set; }
    public List<object> TrailingHeaders { get; set; }
    public RequestMessage RequestMessage { get; set; }
    public bool IsSuccessStatusCode { get; set; }
  }
}
