using Microsoft.AspNetCore.SignalR;

namespace HorseRacingAPI.Hubs
{
    public class RaceHub : Hub
    {
        public async Task JoinRace(string raceId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"race-{raceId}");
        }

        public async Task LeaveRace(string raceId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"race-{raceId}");
        }
    }
}
