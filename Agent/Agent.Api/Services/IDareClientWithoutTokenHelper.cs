using FiveSafesTes.Core.Services;

namespace Agent.Api.Services
{
    public interface IDareClientWithoutTokenHelper: IBaseClientHelper
    {

        bool CheckCredsAreAvailable();


    }
}
