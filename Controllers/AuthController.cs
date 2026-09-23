using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nagorik.Api.Models;

namespace Nagorik.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AuthController(AppDbContext db) { _db = db; }

        [HttpPost("register")]
        public IActionResult Register(RegisterDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            if (_db.Users.Any(u => u.Email == dto.Email))
                return BadRequest(new { message = "Email already registered" });

            var user = new User
            {
                Name = dto.Name,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
            };
            _db.Users.Add(user);
            _db.SaveChanges();
            return Created("", new { user.Id, user.Name, user.Email });
        }

        [HttpPost("login")]
        public IActionResult Login(LoginDto dto)
        {
            var user = _db.Users.FirstOrDefault(u => u.Email == dto.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid email or password" });

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ThisIsMySecretKeyForNagorikPleaseChangeLater123!"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                claims: new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) },
                expires: DateTime.Now.AddDays(7),
                signingCredentials: creds
            );
            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }
    }
}