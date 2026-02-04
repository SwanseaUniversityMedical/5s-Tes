using Agent.Api.Constants;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using Serilog;

namespace Agent.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApprovalController : Controller
    {
        private readonly ApplicationDbContext _DbContext;
        protected readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IKeyCloakService _IKeyCloakService;
        private readonly IEncDecHelper _encDecHelper;
        public IDareSyncHelper _dareSyncHelper { get; set; }
        private readonly IFeatureManager _features;

        public ApprovalController(IDareSyncHelper dareSyncHelper, ApplicationDbContext applicationDbContext,
            IHttpContextAccessor httpContextAccessor, IKeyCloakService IKeyCloakService,
            IEncDecHelper encDecHelper, IFeatureManager features)
        {
            _dareSyncHelper = dareSyncHelper;
            _DbContext = applicationDbContext;
            _httpContextAccessor = httpContextAccessor;
            _IKeyCloakService = IKeyCloakService;
            _encDecHelper = encDecHelper;
            _features = features;
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetMemberships")]
        public List<TreMembershipDecision> GetMemberships(int projectId, bool showOnlyUnprocessed)
        {
                try
                {
                    var users = _DbContext.MembershipDecisions
                        .Include(x => x.User)
                        .Where(x =>
                            (projectId <= 0 || x.Project.Id == projectId) &&
                            (!showOnlyUnprocessed || x.Decision == Decision.Undecided))
                        .ToList();
                    
                    // this logic here is to prevent the duplication of users in the memberships page (if any decisions for a user are Decided (approve/reject), pick the most recent one; otherwise pick the most recent Undecided)
                    // Explanation: there can be many TreMembershipDecision for one user, but if the user can get one Approval decision, the submission from that user can go through
                    // as the logic in the function `IsUserApprovedOnProject` in Agent.Api.Services.SubmissionHelper.cs 
                    var dedupedUsers = users
                        .GroupBy(x => x.User?.Username ?? $"<id:{x.User?.Id}>")
                        .Select(g =>
                        {
                            var mostRecentNonUndecided = g
                                .Where(m => m.Decision != Decision.Undecided)
                                .OrderByDescending(m => m.LastDecisionDate)
                                .FirstOrDefault();

                            return mostRecentNonUndecided ?? g.OrderByDescending(m => m.LastDecisionDate).First();
                        })
                        .ToList();
                    
                    return dedupedUsers;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "{Function} Crash", "GetMemberships");
                    throw;
                }
                
        }


        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetAllTreProjects")]
        public List<TreProject> GetAllTreProjects(bool showOnlyUnprocessed)
        {
            try
            {
                return _DbContext.Projects.Where(x => !showOnlyUnprocessed || x.Decision == Decision.Undecided)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllTreProjects");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetTreProject")]
        public TreProject GetTreProject(int projectId)
        {
            try
            {
                return _DbContext.Projects.First(x => x.Id == projectId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetTreProject");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetAllActiveTreProjects")]
        public List<TreProject> GetAllActiveTreProjects()
        {
            try
            {
                return _DbContext.Projects.Where(x => !x.Archived).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllActiveTreProjects");
                throw;
            }
        }


        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetAllTreUsers")]
        public List<TreUser> GetAllTreUsers()
        {
            try
            {
                return _DbContext.Users.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllTreUsers");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetAllActiveTreUsers")]
        public List<TreUser> GetAllActiveTreUsers()
        {
            try
            {
                return _DbContext.Users.Where(x => !x.Archived).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllActiveTreUsers");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetAllMembershipDecisions")]
        public List<TreMembershipDecision> GetAllMembershipDecisions()
        {
            try
            {
                return _DbContext.MembershipDecisions.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllMembershipDecisions");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetAllActiveMembershipDecisions")]
        public List<TreMembershipDecision> GetAllActiveMembershipDecisions()
        {
            try
            {
                return _DbContext.MembershipDecisions.Where(x => !x.Archived).ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllActiveMembershipDecisions");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("GetAllUndecidedMembershipDecisions")]
        public List<TreMembershipDecision> GetAllUndecidedActiveMembershipDecisions()
        {
            try
            {
                return _DbContext.MembershipDecisions.Where(x => !x.Archived && x.Decision == Decision.Undecided)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllUndecidedActiveMembershipDecisions");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpPost("UpdateProjects")]
        public async Task<List<TreProject>> UpdateProjects(List<TreProject> projects)
        {
            try
            {
                var approvedBy = (from x in User.Claims where x.Type == "preferred_username" select x.Value).First();
                if (string.IsNullOrWhiteSpace(approvedBy))
                {
                    approvedBy = "[Unknown]";
                }

                var resultList = new List<TreProject>();
                var approvedDate = DateTime.Now.ToUniversalTime();
                foreach (var treProject in projects)
                {
                    var dbproj = _DbContext.Projects.First(x => x.Id == treProject.Id);
                    dbproj.LocalProjectName = treProject.LocalProjectName;

                    if (treProject.Password != null)
                    {
                        dbproj.Password = _encDecHelper.Encrypt(treProject.Password);
                    }

                    if (treProject.UserName != null)
                    {
                        dbproj.UserName = treProject.UserName;
                    }

                    if (treProject.Decision != dbproj.Decision)
                    {
                        dbproj.Decision = treProject.Decision;
                        dbproj.ApprovedBy = approvedBy;
                        dbproj.LastDecisionDate = approvedDate;
                    }

                    dbproj.ProjectExpiryDate = treProject.ProjectExpiryDate.ToUniversalTime();


                    resultList.Add(dbproj);
                    await _DbContext.SaveChangesAsync();
                    await ControllerHelpers.AddTreAuditLog(dbproj, null, treProject.Decision == Decision.Approved,
                        _DbContext, _httpContextAccessor, User);
                    if (await _features.IsEnabledAsync(FeatureFlags.GenerateAccounts))
                    {
                        var acc = _DbContext.ProjectAcount.FirstOrDefault(x => x.Name == dbproj.SubmissionProjectName);

                        if (dbproj.Decision == Decision.Approved && acc == null)
                        {
                            await _IKeyCloakService.DoGenAccount(dbproj.SubmissionProjectName);
                        }
                        else if (acc != null && dbproj.Decision != Decision.Approved)
                        {
                            await _IKeyCloakService.DeleteUser(dbproj.SubmissionProjectName);
                        }
                    }

                    Log.Information("{Function}:", "AuditLogs", "Treproject Decision:" + treProject.Decision.ToString(),
                        "ApprovedBy:" + approvedBy);
                }

                await _DbContext.SaveChangesAsync();
                return resultList;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "UpdateProjects");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpPost("UpdateMembershipDecisions")]
        public async Task<List<TreMembershipDecision>> UpdateMembershipDecisions(
            List<TreMembershipDecision> membershipDecisions)
        {
            try
            {
                var approvedBy = (from x in User.Claims where x.Type == "preferred_username" select x.Value).First();
                var returnResult = new List<TreMembershipDecision>();
                if (string.IsNullOrWhiteSpace(approvedBy))
                {
                    approvedBy = "[Unknown]";
                }

                var approvedDate = DateTime.Now.ToUniversalTime();
                foreach (var membershipDecision in membershipDecisions)
                {
                    var dbMembership = _DbContext.MembershipDecisions.First(x => x.Id == membershipDecision.Id);
                    if (membershipDecision.Decision != dbMembership.Decision)
                    {
                        if (await _features.IsEnabledAsync(FeatureFlags.GenerateAccounts))
                        {
                            var name = dbMembership.Project.SubmissionProjectName + dbMembership.User.Username;

                            var acc = _DbContext.ProjectAcount.FirstOrDefault(x => x.Name == name);

                            if (membershipDecision.Decision == Decision.Approved && acc == null)
                            {
                                await _IKeyCloakService.DoGenAccount(name);
                            }
                            else if (acc != null && membershipDecision.Decision != Decision.Approved)
                            {
                                await _IKeyCloakService.DeleteUser(acc.Name);
                            }
                        }

                        dbMembership.Decision = membershipDecision.Decision;
                        dbMembership.ApprovedBy = approvedBy;
                        dbMembership.LastDecisionDate = approvedDate;
                    }

                    returnResult.Add(dbMembership);

                    await _DbContext.SaveChangesAsync();
                    await ControllerHelpers.AddTreAuditLog(null, dbMembership,
                        dbMembership.Decision == Decision.Approved,
                        _DbContext, _httpContextAccessor, User);
                }

                await _DbContext.SaveChangesAsync();
                return returnResult;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "UpdateMembershipDecisions");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("SyncSubmissionWithTre")]
        public async Task<BoolReturn> SyncSubmissionWithTre()
        {
            try
            {
                return await _dareSyncHelper.SyncSubmissionWithTre();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "SyncSubmissionWithTre");
                throw;
            }
        }
    }
}
