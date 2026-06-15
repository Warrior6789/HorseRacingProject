using Microsoft.AspNetCore.Http;

namespace HorseRacingAPI.Services
{
    public interface IHorseImageStorageService
    {
        Task<string> SaveImageAsync(IFormFile image);
    }
}
