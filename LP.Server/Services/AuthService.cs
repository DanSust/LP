using Microsoft.EntityFrameworkCore;

namespace LP.Server.Services
{
    using LP.Entity;
    using Microsoft.CodeAnalysis.Scripting;
    using Microsoft.IdentityModel.Tokens;
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;

    public interface IAuthService
    {
        Task<AuthResponse?> Authenticate(string username, string password);
        Task<AuthResponse> RegisterAsync(string email, string password);
        AuthResponse GenerateToken(User user);
    }

    public class AuthResponse
    { 
        public string Token { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string Email { get; set; } = string.Empty;
    }

    public class AuthService : IAuthService
    {
        private readonly MD5 _md5 = MD5.Create();
        private readonly IConfiguration _config;
        private readonly ApplicationContext _context;
        //private readonly List<User> _users = new()  // In-memory DB for demo
        //{
        //    new User { Id = Guid.NewGuid(), Username = "admin", PasswordHash = _md5.ComputeHash(System.Text.Encoding.ASCII.GetBytes("admin123")) }
        //};

        public AuthService(IConfiguration config, ApplicationContext context)
        {
            _config = config;
            _context = context;

            if (_context.Users.AsNoTracking().SingleOrDefault(u => u.Username == "admin") == null)
            {
                var user = new User
                {
                    Username = "admin",
                    PasswordHash = _md5.ComputeHash(Encoding.ASCII.GetBytes("$Rastainsideus73"))
                };
                _context.Users.Add(user);
                _context.SaveChanges();
            }
        }

        public async Task<AuthResponse?> Authenticate(string username, string password)
        {
            var _hash = _md5.ComputeHash(Encoding.ASCII.GetBytes(password));
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == username);
            if (user == null || !_hash.SequenceEqual(user.PasswordHash))
                return null;

            return GenerateToken(user);
        }

        public async Task<AuthResponse> RegisterAsync(string email, string password)
        {
            // Проверяем, существует ли пользователь
            if (await _context.Users.AnyAsync(u => u.Email == email))
                throw new Exception("Пользователь уже существует");

            // Генерируем соль и хешируем пароль
            var _hash = _md5.ComputeHash(Encoding.ASCII.GetBytes(password));

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = _hash,
                Created = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return GenerateToken(user);
        }


        public AuthResponse GenerateToken(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("ProviderId", user.ProviderId ?? ""), 
                new Claim("name", user.Username ?? user.Caption)
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: credentials
            );

            //return new JwtSecurityTokenHandler().WriteToken(token);
            return new AuthResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                UserId = user.Id,
                Email = user.Email
            };
        }
    }
}
