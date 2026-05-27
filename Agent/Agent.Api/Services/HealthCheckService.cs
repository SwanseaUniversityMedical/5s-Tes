using Agent.Api.Repositories.DbContexts;
using Agent.Api.Models;

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
            return _applicationDbContext.HealthCheckStatus.ToList();
        }
    }
}
