using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;
using ARS.Models;
using ARS.Services;
using System.Linq;

namespace ARS.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthApiController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        public AuthApiController(UserManager<User> userManager, SignInManager<User> signInManager, ITokenService tokenService, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _config = config;
        }

        public class LoginRequest
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class LoginResponse
        {
            public string AccessToken { get; set; } = string.Empty;
            public int ExpiresInSeconds { get; set; }
            public string Email { get; set; } = string.Empty;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { error = "Email and password are required" });
            }

            var user = await _userManager.FindByEmailAsync(req.Email);
            if (user == null)
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            var passOk = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
            if (!passOk.Succeeded)
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = _tokenService.CreateAccessToken(user, roles);
            var expiresMinutes = int.TryParse(_config["JWT:AccessTokenMinutes"], out var m) ? m : 30;

            return Ok(new LoginResponse
            {
                AccessToken = token,
                ExpiresInSeconds = expiresMinutes * 60,
                Email = user.Email ?? string.Empty
            });
        }
    }
}
