using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class OpaAuthorizationMiddleware
    {
        private readonly HttpClient _httpClient;
        private readonly string _opaUrl;

        public OpaAuthorizationMiddleware(RequestDelegate next, string opaUrl)
        {
            _httpClient = new HttpClient();
            _opaUrl = opaUrl;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Evaluate the policy using OPA
            var input = new
            {
                method = context.Request.Method,
                path = context.Request.Path.Value,
                user = context.User?.Identity?.Name ?? "anonymous",
            };

            var requestContent = new StringContent(JsonConvert.SerializeObject(input), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_opaUrl, requestContent);

            if (!response.IsSuccessStatusCode)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<AuthorizationResult>(responseBody);

            // Check if the request is authorized
            //if (result.IsAuthorized)
            //{
            //    await _next(context);
            //}
            //else
            //{
            //    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;

            //}

        }
    }
}
        
