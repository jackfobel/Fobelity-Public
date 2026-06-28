//using Microsoft.AspNetCore.Mvc;
//using System.Security.Claims;
//using System.Text.Json;
//using System.Text;

//namespace MiniSplitControlService
//{


//  public class EasyAuthMiddleware
//  {
//    private readonly RequestDelegate _next;

//    public EasyAuthMiddleware(RequestDelegate next)
//    {
//      _next = next;
//    }

//    public async Task InvokeAsync(HttpContext context)
//    {
//      if (context.Request.Headers.TryGetValue("X-MS-CLIENT-PRINCIPAL", out var header))
//      {
//        var decoded = Convert.FromBase64String(header);
//        var json = Encoding.UTF8.GetString(decoded);
//        var principalData = JsonSerializer.Deserialize<ClientPrincipal>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

//        if (principalData?.UserRoles != null)
//        {
//          var claims = new List<Claim>
//                {
//                    new Claim(ClaimTypes.NameIdentifier, principalData.UserId),
//                    new Claim(ClaimTypes.Name, principalData.UserDetails)
//                };
//          claims.AddRange(principalData.UserRoles.Select(role => new Claim(ClaimTypes.Role, role)));

//          var identity = new ClaimsIdentity(claims, "EasyAuth");
//          context.User = new ClaimsPrincipal(identity);
//        }
//      }

//      await _next(context);
//    }

//    public class ClientPrincipal
//    {
//      public string UserId { get; set; }
//      public string UserDetails { get; set; }
//      public string IdentityProvider { get; set; }
//      public List<string> UserRoles { get; set; }
//    }
//  }



//}
