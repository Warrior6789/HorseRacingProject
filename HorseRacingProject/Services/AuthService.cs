using HorseRacingAPI.Dtos;
using HorseRacingAPI.Enums;
using HorseRacingAPI.Hubs;
using HorseRacingAPI.Models;
using HorseRacingAPI.Repositories;
using HorseRacingAPI.Repository;
using Microsoft.AspNetCore.SignalR;
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
        private readonly ICloudinaryService _cloudinary;
        private readonly IHubContext<RaceHub> _hubContext;

        public AuthService(IUnitofWork unitOfWork, IConfiguration configuration, ICloudinaryService cloudinary, IHubContext<RaceHub> hubContext)
        {
            _unitOfWork = unitOfWork;
            _configuration = configuration;
            _cloudinary = cloudinary;
            _hubContext = hubContext;
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
                throw new InvalidOperationException("Email already exists");

            if (string.IsNullOrWhiteSpace(registerDto.FullName))
                throw new ArgumentException("FullName is required.");
            if (string.IsNullOrWhiteSpace(registerDto.Phone))
                throw new ArgumentException("Phone is required.");

            var newAccount = new Account
            {
                Id = Guid.NewGuid(),
                Email = registerDto.Email,
                PasswordHash = HashPassword(registerDto.Password),
                Role = AccountRole.Spectator,
                Status = AccountStatus.Active,
                CreateAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            };
            await accountRepo.AddAsync(newAccount);

            string? avatarUrl = null;
            if (registerDto.Avatar != null)
                avatarUrl = await _cloudinary.UploadImageAsync(registerDto.Avatar, "avatars");

            await _unitOfWork.GetRepository<UserProfile>().AddAsync(new UserProfile
            {
                ProfileId = Guid.NewGuid(),
                AccountId = newAccount.Id,
                FullName = registerDto.FullName,
                Phone = registerDto.Phone,
                ImageUrl = avatarUrl,
                Balance = 0,
                CreateAt = DateTimeOffset.UtcNow,
                IsDeleted = false
            });

            await _unitOfWork.SaveAsync();
        }

        public async Task RequestRoleUpgradeAsync(Guid accountId, RequestRoleUpgradeDto dto)
        {
            var allowedRoles = new[] { AccountRole.HorseOwner, AccountRole.Jockey, AccountRole.Referee };

            if (!Enum.TryParse<AccountRole>(dto.RequestedRole, ignoreCase: true, out var parsedRole) || !allowedRoles.Contains(parsedRole))
                throw new ArgumentException("Requested role must be HorseOwner, Jockey, or Referee.");

            IGenericRepository<Account> accountRepo = _unitOfWork.GetRepository<Account>();
            Account? account = await accountRepo.Entities.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);

            if (account == null)
                throw new KeyNotFoundException("Account not found.");

            if (account.Role != AccountRole.Spectator || account.Status != AccountStatus.Active)
                throw new InvalidOperationException("Only active Spectator accounts can request a role upgrade.");

            if (account.RequestedRole != null)
                throw new InvalidOperationException("You already have a pending role upgrade request.");

            string? imageUrl = null;
            if (dto.CertificateImage != null)
                imageUrl = await _cloudinary.UploadImageAsync(dto.CertificateImage, "certificates");

            if (parsedRole == AccountRole.Jockey)
            {
                if (string.IsNullOrWhiteSpace(dto.LicenseNumber))
                    throw new ArgumentException("LicenseNumber is required for Jockey.");

                await _unitOfWork.GetRepository<JockeyProfile>().AddAsync(new JockeyProfile
                {
                    JockeyProfileId = Guid.NewGuid(),
                    AccountId = accountId,
                    FullName = dto.FullName,
                    DateOfBirth = dto.DateOfBirth,
                    Nationality = dto.Nationality,
                    LicenseNumber = dto.LicenseNumber,
                    Weight = dto.Weight,
                    Height = dto.Height,
                    ImageUrl = imageUrl,
                    TotalRaces = 0,
                    TotalWins = 0,
                    CreateAt = DateTimeOffset.UtcNow,
                    IsDeleted = false
                });
            }
            else
            {
                UserProfile? userProfile = await _unitOfWork.GetRepository<UserProfile>().Entities
                    .FirstOrDefaultAsync(p => p.AccountId == accountId && !p.IsDeleted);

                if (userProfile == null)
                    throw new InvalidOperationException("User profile not found.");

                if (!string.IsNullOrWhiteSpace(dto.FullName))
                    userProfile.FullName = dto.FullName;
                if (!string.IsNullOrWhiteSpace(dto.Phone))
                    userProfile.Phone = dto.Phone;
                userProfile.CertificateImageUrl = imageUrl;
                userProfile.UpdatedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.GetRepository<UserProfile>().UpdateAsync(userProfile);
            }

            account.RequestedRole = parsedRole;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveAsync();

            int pendingCount = await _unitOfWork.GetRepository<Account>().Entities
                .CountAsync(a => a.RequestedRole != null && !a.IsDeleted);

            await _hubContext.Clients.All.SendAsync("UpgradeRequestsUpdated", new { pendingCount });
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

        public async Task<MeResponse> GetMeAsync(Guid accountId)
        {
            Account? account = await _unitOfWork.GetRepository<Account>().Entities
                .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted);
            if (account == null)
                throw new KeyNotFoundException("Account not found.");

            return new MeResponse
            {
                AccountId     = account.Id,
                Email         = account.Email,
                Role          = account.Role.ToString(),
                RequestedRole = account.RequestedRole?.ToString()
            };
        }
    }
}
