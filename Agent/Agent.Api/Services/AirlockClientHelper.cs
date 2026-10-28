using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Agent.Api.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace Agent.Api.Services
{
    public interface IAirlockClientHelper
    {
        bool CheckCredsAreAvailable();

       
        Task<AirlockFileRequest> SubmitExternalRequestAsync(AirlockExternalRequest request, IReadOnlyList<AirlockUploadFile> files, CancellationToken cancellationToken = default);

       
        Task<string> CheckConnectionAsync();
    }

    /// <summary>
    /// Calls Director-Airlock's AirlockFileRequest API as the Agent's own Keycloak service account
    /// (client-credentials grant, no user and no role).
    /// </summary>
    public class AirlockClientHelper : BaseClientHelper, IAirlockClientHelper
    {
        private readonly AirlockKeyCloakSettings _keycloakSettings;
        private readonly AirlockSettings _airlockSettings;

        public AirlockClientHelper(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor,
            IConfiguration config, IOptionsMonitor<AirlockKeyCloakSettings> keycloakSettings, AirlockSettings airlockSettings)
            : base(httpClientFactory, httpContextAccessor, airlockSettings.ApiUrl.TrimEnd('/'), false)
        {
            _keycloakSettings = keycloakSettings.CurrentValue;
            _airlockSettings = airlockSettings;

            var keycloakDemoMode = KeycloakCommon.ResolveKeycloakDemoMode(_keycloakSettings.KeycloakDemoMode, config["KeycloakDemoMode"]);
            _keycloakTokenHelper = new KeycloakTokenHelper(_keycloakSettings.BaseUrl, _keycloakSettings.ClientId,
                _keycloakSettings.ClientSecret, _keycloakSettings.Proxy, _keycloakSettings.ProxyAddresURL, keycloakDemoMode,
                isServiceAccount: true);

            // The service-account grant ignores the username and password, but BaseClientHelper passes them
            // through, so use the client's own credentials (as the Submission client does). No role is required.
            _username = _keycloakSettings.ClientId;
            _password = _keycloakSettings.ClientSecret;
            _requiredRole = "";
        }

        public bool CheckCredsAreAvailable()
        {
            return !string.IsNullOrEmpty(_keycloakSettings.BaseUrl)
                && !string.IsNullOrEmpty(_keycloakSettings.ClientId)
                && !string.IsNullOrEmpty(_keycloakSettings.ClientSecret);
        }

        public async Task<AirlockFileRequest> SubmitExternalRequestAsync(AirlockExternalRequest request, IReadOnlyList<AirlockUploadFile> files, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json"), "request");

            foreach (AirlockUploadFile file in files)
            {
                var content = new LazyStreamContent(file.OpenStream);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(content, "files", file.Filename);
            }

            HttpClient client = await CreateClientWithKeycloak();
            client.Timeout = TimeSpan.FromMinutes(_airlockSettings.UploadTimeoutMinutes);

            Log.Information("{Function} Sending {FileCount} files to Airlock for {ExternalReference}",
                "SubmitExternalRequestAsync", files.Count, request.ExternalReference);

            using HttpResponseMessage response = await client.PostAsync(_address + "/api/Requests/SubmitExternalRequest", form, cancellationToken);
            return await ReadResponse(response, "SubmitExternalRequest", cancellationToken);
        }


        public async Task<string> CheckConnectionAsync()
        {
            if (string.IsNullOrWhiteSpace(_address))
                return "Airlock API URL is missing.";

            if (!CheckCredsAreAvailable())
                return "Missing Airlock credentials.";

            var token = await _keycloakTokenHelper!.GetTokenForUser(_username, _password, _requiredRole);
            return string.IsNullOrWhiteSpace(token.token)
                ? $"Invalid Airlock credentials: {token.Errorstring}"
                : "";
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_address))
                throw new InvalidOperationException("AirlockSettings:ApiUrl is not set.");

            if (!CheckCredsAreAvailable())
                throw new InvalidOperationException("AirlockKeyCloakSettings BaseUrl, ClientId and ClientSecret must be set (the secret comes from Vault).");
        }

        private static async Task<AirlockFileRequest> ReadResponse(HttpResponseMessage response, string call, CancellationToken cancellationToken)
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("{Function} Airlock {Call} returned {StatusCode}: {Body}", "AirlockClientHelper", call, (int)response.StatusCode, body);
                throw new AirlockApiException(call, response.StatusCode, body);
            }

            return JsonConvert.DeserializeObject<AirlockFileRequest>(body)
                ?? throw new AirlockApiException(call, response.StatusCode, "Empty response body");
        }

        private sealed class LazyStreamContent : HttpContent
        {
            private readonly Func<Task<Stream>> _openStream;

            public LazyStreamContent(Func<Task<Stream>> openStream)
            {
                _openStream = openStream;
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            {
                await using Stream source = await _openStream();
                await source.CopyToAsync(stream);
            }

            protected override bool TryComputeLength(out long length)
            {
                // Unknown length, so the request is sent chunked.
                length = -1;
                return false;
            }
        }
    }

    public class AirlockApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ResponseBody { get; }

        public AirlockApiException(string call, HttpStatusCode statusCode, string responseBody)
            : base($"Airlock {call} failed with {(int)statusCode} {statusCode}: {responseBody}")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
