using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;

namespace Agent.Api.Services
{
    public interface IStatusService
    {
        public List<HealthCheckStatus> GetHealthCheckData();
    }

    public class StatusService : IStatusService
  {
        private readonly ApplicationDbContext _applicationDbContext;

        public StatusService(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public List<HealthCheckStatus> GetHealthCheckData()
        {
            return _applicationDbContext.HealthCheckStatus.ToList();
        }
    }
}
