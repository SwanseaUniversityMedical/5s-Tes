using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace FiveSafesTes.Core.Services
{
    public class DareClientHelper: BaseClientHelper, IDareClientHelper
    {

        public DareClientHelper(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, IConfiguration config): base(httpClientFactory, httpContextAccessor, config["DareAPISettings:Address"], false)
        {
        
        }
    }
}
