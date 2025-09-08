using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string receptor, string subject, string body);
        Task SendInvitationEmailAsync(string email, string token, Guid companyId);
        Task SendWelcomeEmailAsync(string email, string fullName, string companyName);
    }
}
