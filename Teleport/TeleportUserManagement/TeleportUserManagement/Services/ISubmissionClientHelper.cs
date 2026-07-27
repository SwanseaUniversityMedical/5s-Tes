using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.Extensions.Options;

namespace TeleportUserManagement.Services;

public interface ISubmissionClientHelper : IBaseClientHelper
{
    bool CheckCredsAreAvailable();
}

public class SubmissionClientHelper : BaseClientHelper, ISubmissionClientHelper
{
    private readonly SubmissionKeyCloakSettings _keycloakSettings;

    public SubmissionClientHelper(
        IHttpClientFactory httpClientFactory,
        IHttpContextAccessor httpContextAccessor,
        IEncDecHelper encDec,
        IOptionsMonitor<SubmissionKeyCloakSettings> keycloakSettings,
        IOptions<ApiEndpointSettings> apiEndpointSettings)
        : base(httpClientFactory, httpContextAccessor, apiEndpointSettings.Value.SubmissionApiUrl, false)
    {
        _keycloakSettings = keycloakSettings.CurrentValue;

        bool useServiceAccount = _keycloakSettings.ConfigInputMethod == ConfigInputMethod.Upload;

        _keycloakTokenHelper = new KeycloakTokenHelper(
            _keycloakSettings.BaseUrl, _keycloakSettings.ClientId, _keycloakSettings.ClientSecret,
            _keycloakSettings.Proxy, _keycloakSettings.ProxyAddresURL, _keycloakSettings.KeycloakDemoMode, useServiceAccount);

        if (CheckCredsAreAvailable())
        {
            _username = _keycloakSettings.Username;
            _password = encDec.Decrypt(_keycloakSettings.PasswordEnc);
            _requiredRole = "dare-tre-admin";
        }
    }

    public bool CheckCredsAreAvailable()
    {
        return !string.IsNullOrEmpty(_keycloakSettings.Username) && !string.IsNullOrEmpty(_keycloakSettings.PasswordEnc);
    }
}
