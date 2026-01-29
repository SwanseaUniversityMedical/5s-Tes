using Microsoft.AspNetCore.SignalR;

namespace Agent.Api.Services.SignalR
{
    public interface ISignalRService
    {
        Task SendUpdateMessage(string updateName, List<string> varList);
    }

    public class SignalRService : Hub, ISignalRService
    {
        private readonly IHubContext<SignalRService> _hubContext;

        public SignalRService(IHubContext<SignalRService> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendUpdateMessage(string updateName, List<string> varList)
        {
            await _hubContext.Clients.All.SendAsync(updateName, varList);
        }
    }
}
