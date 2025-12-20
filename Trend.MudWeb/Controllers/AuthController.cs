using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Trend.MudWeb.Models;

namespace Trend.MudWeb.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly TrendRepo _repo;

        public AuthController(TrendRepo repo)
        {
            _repo = repo;
        }

        /// <summary>
        /// Menangani proses Login dan pembuatan Cookie Authentication.
        /// Memasukkan status langganan ke dalam Claim untuk Otorisasi.
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] string email, [FromForm] string password)
        {
            // Validasi user dari database
            var user = await _repo.GetUserByEmailAsync(email);

            if (user != null && user.PasswordHash == password) // Note: Gunakan BCrypt untuk produksi
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim("UserId", user.UserId.ToString()),
                    // Menyimpan status langganan sebagai Claim untuk Otorisasi 
                    new Claim("SubscriptionStatus", user.SubscriptionStatus ?? "Free")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                return LocalRedirect("/dashboard");
            }

            return Redirect("/login?error=InvalidCredentials");
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return LocalRedirect("/");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromForm] string username, [FromForm] string email, [FromForm] string password, [FromForm] int nicheId)
        {
            // Cek apakah email sudah terdaftar
            var existingUser = await _repo.GetUserByEmailAsync(email);
            if (existingUser != null)
            {
                return Redirect("/register?error=EmailExists");
            }

            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = password, // Disarankan menggunakan hashing seperti BCrypt
                NicheId = nicheId,
                SubscriptionStatus = "Free", // Default sebagai user gratis
                CreatedAt = DateTime.Now
            };

            await _repo.AddUserAsync(newUser); // Pastikan metode ini ada di TrendRepo
            return Redirect("/login?registerSuccess=true");
        }
    }
}