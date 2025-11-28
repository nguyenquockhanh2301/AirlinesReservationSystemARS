using ARS.Models;
using System.Collections.Generic;

namespace ARS.Services
{
    public interface ITokenService
    {
        string CreateAccessToken(User user, IEnumerable<string> roles);
    }
}
