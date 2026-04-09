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
                var projectCountTask = _dbContext.Projects.AsNoTracking().CountAsync();
                var submissionCountTask = _dbContext.Submissions.AsNoTracking().CountAsync(x => x.ParentId == null);
                var userCountTask = _dbContext.Users.AsNoTracking().CountAsync();
                var treCountTask = _dbContext.Tres.AsNoTracking().CountAsync();

                await Task.WhenAll(projectCountTask, submissionCountTask, userCountTask, treCountTask);

                return new DashboardCounts
                {
                    ProjectCount = projectCountTask.Result,
                    SubmissionCount = submissionCountTask.Result,
                    UserCount = userCountTask.Result,
                    TreCount = treCountTask.Result,
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

                var projectCountTask = _dbContext.Projects.AsNoTracking().CountAsync();
                var submissionCountTask = _dbContext.Submissions.AsNoTracking().CountAsync(x => x.ParentId == null);
                var userCountTask = _dbContext.Users.AsNoTracking().CountAsync();
                var treCountTask = _dbContext.Tres.AsNoTracking().CountAsync();
                var userOnProjectCountTask = _dbContext.Projects.AsNoTracking().CountAsync(x => x.Users.Any(u => u.Name.ToLower() == preferredUsername));
                var userWroteSubmissionCountTask = _dbContext.Submissions.AsNoTracking().CountAsync(x => x.ParentId == null && x.SubmittedBy != null && x.SubmittedBy.Name.ToLower() == preferredUsername);

                await Task.WhenAll(projectCountTask, submissionCountTask, userCountTask, treCountTask, userOnProjectCountTask, userWroteSubmissionCountTask);

                return new DashboardCounts
                {
                    ProjectCount = projectCountTask.Result,
                    SubmissionCount = submissionCountTask.Result,
                    UserCount = userCountTask.Result,
                    TreCount = treCountTask.Result,
                    UserOnProjectCount = userOnProjectCountTask.Result,
                    UserWroteSubmissionCount = userWroteSubmissionCountTask.Result,
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