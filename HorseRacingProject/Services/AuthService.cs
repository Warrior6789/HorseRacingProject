using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

namespace HorseRacingAPI.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUnitofWork _unitOfWork;
        private readonly IConfiguration _configuration;
        public AuthService(IUnitofWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
        }

        public async Task<string?> LoginAsync(LoginDto loginDto)
        {
            IGenericRepository<Account> accountRepo = _unitOfWork.GetRepository<Account>();
            Account? acc = await accountRepo.Entities.FirstOrDefaultAsync(a => a.Email == loginDto.Email);

            if (acc == null || !VerifyPassword(loginDto.Password, acc.PasswordHash))
            {
                return null;
            }
            
            if (acc.Status == AccountStatus.Banned)
            {
                throw new InvalidOperationException("Your account has been banned.");
            }
            if (acc.Status != AccountStatus.Active)
            {
                return null;
            }
            string token = GenerateJwtToken(acc);
            return token;
        }

        public async Task RegisterAsync(RegisterDto registerDto)
        {
            IGenericRepository<Account> accountRepo = _unitOfWork.GetRepository<Account>();
            bool emailExists = await accountRepo.Entities.AnyAsync(a => a.Email == registerDto.Email);
            if (emailExists)
            {
                throw new InvalidOperationException("Email already exists");
            }

            AccountStatus status = AccountStatus.Pending;
            if (string.IsNullOrWhiteSpace(registerDto.RequestedRole) ||
    string.Equals(registerDto.RequestedRole, AccountRole.Spectator.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                status = AccountStatus.Active;
            }
            Enum.TryParse<AccountRole>(registerDto.RequestedRole, true, out var parsedRole);
            var newAccount = new Account
            {
                Email = registerDto.Email,
                PasswordHash = HashPassword(registerDto.Password),
                Role = string.IsNullOrWhiteSpace(registerDto.RequestedRole) ? AccountRole.Spectator : parsedRole,
                Status = status,
                CreateAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };
            await accountRepo.AddAsync(newAccount);
            await _unitOfWork.SaveAsync();
        }

        private string GenerateJwtToken(Account account)
        {
            var key = _configuration["Jwt:Key"] ?? throw new Exception("Missing config: Jwt:Key");
            string? issuer = _configuration["Jwt:Issuer"];
            string? audience = _configuration["Jwt:Audience"];
            var expireMinutes = int.TryParse(_configuration["Jwt:DurationInMinutes"], out var m) ? m : 180;

            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), 
        new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
        new Claim(ClaimTypes.Email, account.Email),
        new Claim(ClaimTypes.Role, account.Role.ToString()) 
    };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(expireMinutes), 
                SigningCredentials = creds,
                Issuer = string.IsNullOrWhiteSpace(issuer) ? null : issuer,     
                Audience = string.IsNullOrWhiteSpace(audience) ? null : audience 
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        private string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be empty.");
            }

            using (var hmac = new HMACSHA512())
            {
                byte[] salt = hmac.Key;
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

                byte[] hashBytes = new byte[salt.Length + hash.Length];
                Array.Copy(salt, 0, hashBytes, 0, salt.Length);
                Array.Copy(hash, 0, hashBytes, salt.Length, hash.Length);

                return Convert.ToBase64String(hashBytes);
            }
        }

            private bool VerifyPassword(string password, string storedHashBase64)
        {
            try
            {
                byte[] hashBytes = Convert.FromBase64String(storedHashBase64);
                if (hashBytes.Length < 128) return false;

                byte[] salt = new byte[128];
                Array.Copy(hashBytes, 0, salt, 0, salt.Length);

                using (var hmac = new HMACSHA512(salt))
                {
                    byte[] computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));

                    for (int i = 0; i < computedHash.Length; i++)
                    {
                        if (computedHash[i] != hashBytes[salt.Length + i]) return false;
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
