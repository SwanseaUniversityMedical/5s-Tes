using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace FiveSafesTes.Core.Services
{
    public class DataEgressClientHelper : BaseClientHelper, IDataEgressClientHelper
    {
        public DataEgressClientHelper(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, IConfiguration config) : base(httpClientFactory, httpContextAccessor, config["DataEgressAPISettings:Address"], false)
        {

        }
    }
}
