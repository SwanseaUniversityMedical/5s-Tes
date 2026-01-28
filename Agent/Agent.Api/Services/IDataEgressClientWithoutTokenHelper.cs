using FiveSafesTes.Core.Services;

namespace Agent.Api.Services
{
  public interface IDataEgressClientWithoutTokenHelper : IBaseClientHelper
  {
    bool CheckCredsAreAvailable();
  }
}
