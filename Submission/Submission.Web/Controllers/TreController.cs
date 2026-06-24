using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Newtonsoft.Json;
using System.Security.Claims;

namespace Submission.Web.Controllers
{
    [Authorize]
    public class TreController : Controller
    {
        private readonly IDareClientHelper _clientHelper;

        private readonly FormIOSettings _formIOSettings;

        public TreController(IDareClientHelper client, FormIOSettings formIo)
        {
            _clientHelper = client;

            _formIOSettings = formIo;
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public IActionResult SaveATre(int treId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var formData = new FormData()
            {
                FormIoUrl = _formIOSettings.TreForm,
                FormIoString = @"{""id"":0}",
                Id = treId
            };

            if (treId > 0)
            {
                var paramList = new Dictionary<string, string>();
                paramList.Add("treId", treId.ToString());
                var tre = _clientHelper.CallAPIWithoutModel<Tre>("/api/Tre/GetATre/", paramList).Result;
                formData.FormIoString = tre?.FormData;
                formData.FormIoString = formData.FormIoString?.Replace(@"""id"":0", @"""Id"":" + treId.ToString(),
                    StringComparison.CurrentCultureIgnoreCase);
            }

            return View(formData);
        }


        [HttpGet]
        public IActionResult GetAllTres()
        {
            var queryParams = new Dictionary<string, string>
            {
              ["responseType"] = "summary",
            };
            var tres = _clientHelper.CallAPIWithoutModel<List<Tre.TreSummary>>("/api/Tre/GetAllTres/", queryParams).Result;

            return View(tres);
        }

        [HttpGet]
        public IActionResult GetATre(int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string>();
            paramlist.Add("treId", id.ToString());
            paramlist.Add("responseType", "summary");
            var Tre = _clientHelper.CallAPIWithoutModel<Tre.TreDetailsDto>(
                "/api/Tre/GetATre/", paramlist).Result;

            if (Tre == null)
            {
                return NotFound();
            }

            return View(Tre);
        }

        [HttpGet]
        [Authorize(Roles = "dare-tre-admin")]
        public async Task<IActionResult> DownloadConfig(int treId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string> { { "treId", treId.ToString() } };
            var tre = await _clientHelper.CallAPIWithoutModel<Tre?>("/api/Tre/GetATre/", paramlist);
            if (tre == null)
            {
                return NotFound($"TRE with id {treId} not found");
            }

            var username = User.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
            var isTreAdmin = !string.IsNullOrEmpty(username) &&
                             !string.IsNullOrEmpty(tre.AdminUsername) &&
                             string.Equals(tre.AdminUsername, username, StringComparison.OrdinalIgnoreCase);

            if (!isTreAdmin)
            {
                return Forbid();
            }

            var bytes = await _clientHelper.CallAPIToGetFile($"/api/Tre/DownloadConfig/{treId}");
            var fileName = $"tre-onboarding-{tre.Name.ToLowerInvariant()}.json";
            return File(bytes, "application/json", fileName);
        }


        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> TreFormSubmission([FromBody] object arg, int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var str = arg?.ToString();

            if (!string.IsNullOrEmpty(str))
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<FormData>(str);
                data.FormIoString = str;


                var json = System.Text.Json.JsonSerializer.Serialize(data);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _clientHelper.CallAPI("/api/Tre/SaveTre", content);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    var tre = JsonConvert.DeserializeObject<Tre>(result);
                    TempData["success"] = "Tre saved Successfully";
                    return Ok(tre);
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();
                    return BadRequest(errorMessage);
                }
            }

            return BadRequest("Invalid data");
        }
    }
}
