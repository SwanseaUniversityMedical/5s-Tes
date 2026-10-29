using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Submission.Api.Attributes;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace Submission.Api.Controllers
{
    /// <summary>
    /// Orchestrator API for GA4GH TES tasks in the federated profile: queue a task for a TRE, let the
    /// TRE Agent poll for and report on its tasks, and read a task back.
    /// </summary>
    [ApiExplorerSettings(GroupName = "v1")]
    [Route("api/[controller]")]
    [ApiController]
    public class FederatedTaskController : Controller
    {
        /// <summary>Upper bound on tasks returned to a TRE Agent in a single poll.</summary>
        private const int MaxPendingTasks = 50;

        private readonly ApplicationDbContext _DbContext;

        public FederatedTaskController(ApplicationDbContext db)
        {
            _DbContext = db;
        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost]
        [Route("Enqueue")]
        [ValidateModelState]
        [SwaggerOperation("Enqueue")]
        [SwaggerResponse(statusCode: 200, type: typeof(TesCreateTaskResponse), description: "")]
        public virtual async Task<IActionResult> Enqueue([FromBody] TesTask task, CancellationToken cancellationToken = default)
        {
            var parsed = FederatedTesProfile.Parse(task);
            if (!parsed.IsSuccess)
            {
                return BadRequest($"{parsed.ErrorCode}: {parsed.ErrorMessage}");
            }

            var spec = parsed.Spec!;
            var tre = await _DbContext.Tres
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == spec.TreName, cancellationToken);
            if (tre == null)
            {
                return BadRequest($"{FederatedTaskErrorCodes.InvalidTask}: TRE '{spec.TreName}' doesn't exist");
            }

            // Server-owned fields: callers must not be able to inject state, logs or results into a queued task.
            task.Id = null;
            task.State = default;
            task.Logs = null;
            task.CreationTime = null;

            var entity = new FederatedTask
            {
                TaskId = Guid.NewGuid(),
                TreId = tre.Id,
                StudyId = spec.StudyId,
                Operation = spec.Operation,
                Dataset = spec.Dataset,
                TesTaskJson = JsonConvert.SerializeObject(task),
                State = TesState.QUEUEDEnum,
                SubmittedAt = DateTime.UtcNow
            };
            _DbContext.FederatedTasks.Add(entity);
            await _DbContext.SaveChangesAsync(cancellationToken);

            return Ok(new TesCreateTaskResponse { Id = entity.TaskId.ToString() });
        }

        [Authorize(Roles = "dare-control-admin,dare-tre-admin")]
        [HttpGet]
        [Route("GetPendingTasksForTre")]
        [ValidateModelState]
        [SwaggerOperation("GetPendingTasksForTre")]
        [SwaggerResponse(statusCode: 200, type: typeof(List<TesTask>), description: "")]
        public virtual async Task<IActionResult> GetPendingTasksForTre(CancellationToken cancellationToken = default)
        {
            var tre = ControllerHelpers.GetUserTre(User, _DbContext);

            var tasks = await _DbContext.FederatedTasks
                .AsNoTracking()
                .Where(x => x.TreId == tre!.Id && x.State == TesState.QUEUEDEnum)
                .OrderBy(x => x.SubmittedAt)
                .Take(MaxPendingTasks)
                .ToListAsync(cancellationToken);

            return Ok(tasks.Select(x => x.ToTesTask()).ToList());
        }

        [Authorize(Roles = "dare-control-admin,dare-tre-admin")]
        [HttpPost]
        [Route("ReportResult")]
        [ValidateModelState]
        [SwaggerOperation("ReportResult")]
        [SwaggerResponse(statusCode: 200, type: typeof(BoolReturn), description: "")]
        public virtual async Task<IActionResult> ReportResult([FromBody] FederatedTaskResultReport report, CancellationToken cancellationToken = default)
        {
            if (report.State is not (TesState.COMPLETEEnum or TesState.EXECUTORERROREnum or TesState.SYSTEMERROREnum))
            {
                return BadRequest($"{FederatedTaskErrorCodes.InvalidTask}: state must be COMPLETE, EXECUTOR_ERROR or SYSTEM_ERROR");
            }

            if (!Guid.TryParse(report.TaskId, out var taskId))
            {
                return BadRequest($"{FederatedTaskErrorCodes.InvalidTask}: TaskId must be a GUID");
            }

            var tre = ControllerHelpers.GetUserTre(User, _DbContext);

            // A task that belongs to another TRE is indistinguishable from one that doesn't exist.
            var task = await _DbContext.FederatedTasks
                .FirstOrDefaultAsync(x => x.TaskId == taskId && x.TreId == tre!.Id, cancellationToken);
            if (task == null)
            {
                return NotFound();
            }

            if (task.State != TesState.QUEUEDEnum)
            {
                return Conflict();
            }

            task.State = report.State;
            task.CompletedAt = DateTime.UtcNow;
            if (report.State == TesState.COMPLETEEnum)
            {
                task.ResultValue = report.Value;
                task.ResultRowCount = report.RowCount;
            }
            else
            {
                task.ErrorCode = report.ErrorCode;
                task.ErrorMessage = report.ErrorMessage;
            }

            await _DbContext.SaveChangesAsync(cancellationToken);
            return Ok(new BoolReturn { Result = true });
        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpGet]
        [Route("GetTask")]
        [ValidateModelState]
        [SwaggerOperation("GetTask")]
        [SwaggerResponse(statusCode: 200, type: typeof(TesTask), description: "")]
        public virtual async Task<IActionResult> GetTask([FromQuery] string id, CancellationToken cancellationToken = default)
        {
            if (!Guid.TryParse(id, out var taskId))
            {
                return NotFound();
            }

            var task = await _DbContext.FederatedTasks
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TaskId == taskId, cancellationToken);
            if (task == null)
            {
                return NotFound();
            }

            return Ok(task.ToTesTask());
        }
    }
}
