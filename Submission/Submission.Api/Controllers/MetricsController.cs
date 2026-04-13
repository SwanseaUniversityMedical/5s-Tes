using FiveSafesTes.Core.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Submission.Api.Repositories.DbContexts;

namespace Submission.Api.Controllers
{
    [ApiExplorerSettings(GroupName = "v1")]
    [ApiController]
    [Route("api/[controller]")]
    public class MetricsController : ControllerBase
    {
        private readonly ApplicationDbContext _dbContext;

        public MetricsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        
        [AllowAnonymous]
        [HttpGet("GetPublicCounts")]
        public async Task<DashboardCounts> GetPublicCounts()
        {
            try
            {
                var projectCount = await _dbContext.Projects.AsNoTracking().CountAsync();
                var submissionCount = await _dbContext.Submissions.AsNoTracking().CountAsync(x => x.Parent == null);
                var userCount = await _dbContext.Users.AsNoTracking().CountAsync();
                var treCount = await _dbContext.Tres.AsNoTracking().CountAsync();

                return new DashboardCounts
                {
                    ProjectCount = projectCount,
                    SubmissionCount = submissionCount,
                    UserCount = userCount,
                    TreCount = treCount,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetPublicCounts");
                throw;
            }
        }
    }
}
