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
                var submissionCount = await _dbContext.Submissions.AsNoTracking().CountAsync(x => x.ParentId == null);
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

        [Authorize]
        [HttpGet("GetCurrentUserCounts")]
        public async Task<DashboardCounts> GetCurrentUserCounts()
        {
            try
            {
                var preferredUsername = (from x in User.Claims where x.Type == "preferred_username" select x.Value).First().ToLower();

                var projectCount = await _dbContext.Projects.AsNoTracking().CountAsync();
                var submissionCount = await _dbContext.Submissions.AsNoTracking().CountAsync(x => x.ParentId == null);
                var userCount = await _dbContext.Users.AsNoTracking().CountAsync();
                var treCount = await _dbContext.Tres.AsNoTracking().CountAsync();
                var userOnProjectCount = await _dbContext.Projects.AsNoTracking().CountAsync(x => x.Users.Any(u => u.Name.ToLower() == preferredUsername));
                var userWroteSubmissionCount = await _dbContext.Submissions.AsNoTracking().CountAsync(x => x.ParentId == null && x.SubmittedBy != null && x.SubmittedBy.Name.ToLower() == preferredUsername);

                return new DashboardCounts
                {
                    ProjectCount = projectCount,
                    SubmissionCount = submissionCount,
                    UserCount = userCount,
                    TreCount = treCount,
                    UserOnProjectCount = userOnProjectCount,
                    UserWroteSubmissionCount = userWroteSubmissionCount,
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetCurrentUserCounts");
                throw;
            }
        }
    }
}