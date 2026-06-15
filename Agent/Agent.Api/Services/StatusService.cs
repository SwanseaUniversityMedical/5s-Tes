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
            // Return only the most recent log for each product.
            return _applicationDbContext.HealthCheckStatus.GroupBy(x => x.Product).Select(g => g.OrderByDescending(x => x.DateTime).First()).ToList();
        }
    }
}
