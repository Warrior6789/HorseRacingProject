using HorseRacingAPI.Dtos;

namespace HorseRacingAPI.Services
{
    public interface IRegistrationService
    {
        Task<List<RegistrationResponse>> GetMyRequestAsync(Guid jockeyAccountId);
        Task<PagedResponse<RegistrationResponse>> GetMyRequestPagedAsync(Guid jockeyAccountId, int page, int pageSize);
        Task<List<RegistrationResponse>> GetOwnerRequestAsync(Guid ownerAccountId);
        Task<PagedResponse<RegistrationResponse>> GetOwnerRequestPagedAsync(Guid ownerAccountId, int page, int pageSize);
        Task<List<RegistrationResponse>> GetAllOwnerRegistrationsAsync(Guid ownerAccountId);
        Task<PagedResponse<RegistrationResponse>> GetAllOwnerRegistrationsPagedAsync(Guid ownerAccountId, int page, int pageSize);
        Task AcceptRegistrationAsync(Guid registrationId, Guid jockeyAccountId);
        Task RejectRegistrationAsync(Guid registrationId, Guid jockeyAccountId);
        Task ScratchHorseAsync(Guid registrationId);
        Task AdminAcceptRegistrationAsync(Guid registrationId);
        Task AdminRejectRegistrationAsync(Guid registrationId);
        Task<PagedResponse<RegistrationResponse>> GetAllRegistrationsPagedAsync(int page, int pageSize);
    }
}
