using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRegistrationService
    {
        Task<List<RegistrationResponse>> GetMyRequestAsync(Guid jockeyAccountId);
        Task<PagedResponse<RegistrationResponse>> GetMyRequestPagedAsync(Guid jockeyAccountId, int page, int pageSize);
        Task AcceptRegistrationAsync(Guid registrationId, Guid jockeyAccountId);
        Task RejectRegistrationAsync(Guid registrationId, Guid jockeyAccountId);
        Task ScratchHorseAsync(Guid registrationId);
    }
}
