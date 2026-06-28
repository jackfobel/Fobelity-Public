using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainModels.Email.Interfaces
{
  public interface IEmailService
  {
    Task SendEmailAsync(string subject, string body, string? to = null);
  }


}
