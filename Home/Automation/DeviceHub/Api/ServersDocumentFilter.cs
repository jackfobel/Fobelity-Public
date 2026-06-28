using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Fobelity.Home.Automation.DeviceHub.Api
{
  public sealed class ServersDocumentFilter : IDocumentFilter
  {
    private readonly IConfiguration _cfg;
    public ServersDocumentFilter(IConfiguration cfg) => _cfg = cfg;

    public void Apply(OpenApiDocument doc, DocumentFilterContext ctx)
    {
      var url = _cfg["SWAGGER_SERVER_URL"];
      if (!string.IsNullOrWhiteSpace(url))
        doc.Servers = new List<OpenApiServer> { new() { Url = url.TrimEnd('/') + "/" } };
    }
  }

}
