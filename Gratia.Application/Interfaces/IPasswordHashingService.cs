using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gratia.Application.Interfaces
{
    public interface IPasswordHashingService
    {
        string HashPassword(string password);
        // With no hash (no account has the email), checks the password against a dummy hash and answers false, so it
        // takes as long as a wrong password does and the response time doesn't tell whether an account exists (#82)
        bool VerifyPassword(string password, string? hashedPassword);
    }
}
