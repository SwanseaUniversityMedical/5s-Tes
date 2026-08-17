using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;

namespace Agent.Api.Services
{
    public interface IHealthCheckService
    {
        public List<HealthCheckStatus> GetHealthCheckData();
    }

    public class HealthCheckService : IHealthCheckService
  {
        private readonly ApplicationDbContext _applicationDbContext;

        public HealthCheckService(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }

        public List<HealthCheckStatus> GetHealthCheckData()
        {
            DateTime logCutoff = DateTime.UtcNow.AddHours(-24);
            // Only return logs from the last 24 hours.
            return _applicationDbContext.HealthCheckStatus.Where(x => x.DateTime >= logCutoff).OrderByDescending(x => x.DateTime).ToList();
        }
    }
}
