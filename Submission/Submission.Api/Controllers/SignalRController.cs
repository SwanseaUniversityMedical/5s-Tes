using FiveSafesTes.Core.Models.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;


namespace Submission.Api.Controllers
{
    [ApiController]
    
    [Route("api/[controller]")]
    public class SignalRController : Controller
    {
        HubConnection connection;
        private readonly TREAPISettings _APISettings;

        public SignalRController(IOptions<TREAPISettings> APISettings)
        {
            _APISettings = APISettings.Value;
        }

        [Authorize(Roles = "dare-tre,dare-control-admin")]
        [HttpPost("Index")]
        public async Task<IActionResult> Index()
        {
            
            connection = new HubConnectionBuilder()
                .WithUrl(_APISettings.SignalRAddress)
                .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

            connection.On<string, string>("TREMessage", (user, message) =>
            {
                //TODO:  Code for picking up new subs goes here
            });
            await connection.StartAsync();

            return View();
        }

        [Authorize(Roles = "dare-tre,dare-control-admin")]
        [HttpGet("StartConnection")]
        public IActionResult StartConnection()
        {

            return View();
        }
    }
}
