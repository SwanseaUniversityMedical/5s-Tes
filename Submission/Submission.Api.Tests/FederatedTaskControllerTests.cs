using System.Security.Claims;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Submission.Api.Controllers;
using Submission.Api.Repositories.DbContexts;
using Xunit;

namespace Submission.Api.Tests
{
    public class FederatedTaskControllerTests
    {
        private static ApplicationDbContext NewDb()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new ApplicationDbContext(options);
            db.Tres.Add(NewTre(1, "tre-a", "tre-a-admin"));
            db.Tres.Add(NewTre(2, "tre-b", "tre-b-admin"));
            db.SaveChanges();
            return db;
        }

        private static Tre NewTre(int id, string name, string admin) => new()
        {
            Id = id,
            Name = name,
            AdminUsername = admin,
            About = "",
            FormData = ""
        };

        private static FederatedTaskController ControllerFor(ApplicationDbContext db, string username)
        {
            var identity = new ClaimsIdentity([new Claim("preferred_username", username)], "test");
            return new FederatedTaskController(db)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
                }
            };
        }

        private static TesTask MeanTask(string tre = "tre-a", string dataset = "cohort_2024") =>
            FederatedTesProfile.Build(new FederatedTaskSpec(
                "mean", dataset, "age", [], "study-1", tre));

        private static FederatedTask SeedTask(ApplicationDbContext db, int treId, TesState state, TesTask? task = null)
        {
            var entity = new FederatedTask
            {
                TaskId = Guid.NewGuid(),
                TreId = treId,
                StudyId = "study-1",
                Operation = "mean",
                Dataset = "cohort_2024",
                TesTaskJson = JsonConvert.SerializeObject(task ?? MeanTask()),
                State = state,
                SubmittedAt = DateTime.UtcNow
            };
            db.FederatedTasks.Add(entity);
            db.SaveChanges();
            return entity;
        }

        [Fact]
        public async Task Enqueue_Valid_PersistsQueuedTask()
        {
            using var db = NewDb();
            var result = await ControllerFor(db, "dare-admin").Enqueue(MeanTask());

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<TesCreateTaskResponse>(ok.Value);
            Assert.True(Guid.TryParse(response.Id, out var id));
            var row = db.FederatedTasks.Single();
            Assert.Equal(id, row.TaskId);
            Assert.Equal(TesState.QUEUEDEnum, row.State);
            Assert.Equal(1, row.TreId);
            Assert.Equal("cohort_2024", row.Dataset);
        }

        public static IEnumerable<object[]> OutsideProfile()
        {
            var image = MeanTask();
            image.Executors[0].Image = "ubuntu:22.04";
            yield return [image];

            var multi = MeanTask();
            multi.Tags!["tres"] = "tre-a|tre-b";
            yield return [multi];

            var unknown = MeanTask();
            unknown.Tags!["tres"] = "tre-zzz";
            yield return [unknown];

            var output = MeanTask();
            output.Outputs = [new TesOutput { Url = "s3://x/y", Path = "/out" }];
            yield return [output];
        }

        [Theory]
        [MemberData(nameof(OutsideProfile))]
        public async Task Enqueue_RejectsOutsideProfile(TesTask task)
        {
            using var db = NewDb();
            var result = await ControllerFor(db, "dare-admin").Enqueue(task);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.StartsWith(FederatedTaskErrorCodes.InvalidTask, Assert.IsType<string>(bad.Value));
            Assert.Equal(0, db.FederatedTasks.Count());
        }

        [Fact]
        public async Task Enqueue_TreLookupIsCaseInsensitive()
        {
            using var db = NewDb();
            var task = MeanTask();
            task.Tags!["tres"] = "TRE-A";
            var result = await ControllerFor(db, "dare-admin").Enqueue(task);

            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(1, db.FederatedTasks.Single().TreId);
        }

        [Fact]
        public async Task Enqueue_StripsServerOwnedFields()
        {
            using var db = NewDb();
            var task = MeanTask();
            task.Id = "spoofed";
            task.State = TesState.COMPLETEEnum;
            task.CreationTime = DateTimeOffset.UtcNow.AddDays(-1);
            task.Logs = [new TesTaskLog { Metadata = new Dictionary<string, string> { ["value"] = "999" } }];

            await ControllerFor(db, "dare-admin").Enqueue(task);

            var pending = Assert.IsType<OkObjectResult>(
                await ControllerFor(db, "tre-a-admin").GetPendingTasksForTre());
            var returned = Assert.Single(Assert.IsType<List<TesTask>>(pending.Value));
            Assert.Null(returned.Logs);
            Assert.Equal(TesState.QUEUEDEnum, returned.State);
            Assert.NotEqual("spoofed", returned.Id);
        }

        [Fact]
        public async Task GetPending_ReturnsOnlyCallersQueuedTasks()
        {
            using var db = NewDb();
            var mine = SeedTask(db, 1, TesState.QUEUEDEnum);
            SeedTask(db, 1, TesState.COMPLETEEnum);
            SeedTask(db, 2, TesState.QUEUEDEnum);

            var result = await ControllerFor(db, "tre-a-admin").GetPendingTasksForTre();

            var ok = Assert.IsType<OkObjectResult>(result);
            var task = Assert.Single(Assert.IsType<List<TesTask>>(ok.Value));
            Assert.Equal(mine.TaskId.ToString(), task.Id);
            Assert.Equal(TesState.QUEUEDEnum, task.State);
            Assert.Equal("mean", task.Executors[0].Command[0]);
        }

        private static async Task<TesTask> GetTask(ApplicationDbContext db, Guid id)
        {
            var result = await ControllerFor(db, "dare-admin").GetTask(id.ToString());
            return Assert.IsType<TesTask>(Assert.IsType<OkObjectResult>(result).Value);
        }

        [Fact]
        public async Task ReportResult_Complete_ThenGetTaskShowsResult()
        {
            using var db = NewDb();
            var seeded = SeedTask(db, 1, TesState.QUEUEDEnum);

            var result = await ControllerFor(db, "tre-a-admin").ReportResult(new FederatedTaskResultReport
            {
                TaskId = seeded.TaskId.ToString(),
                State = TesState.COMPLETEEnum,
                Value = 40.5m,
                RowCount = 2
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.True(Assert.IsType<BoolReturn>(ok.Value).Result);
            var task = await GetTask(db, seeded.TaskId);
            Assert.Equal(TesState.COMPLETEEnum, task.State);
            Assert.Equal("40.5", task.Logs![0].Metadata["value"]);
            Assert.Equal("2", task.Logs[0].Metadata["row_count"]);
        }

        [Fact]
        public async Task ReportResult_Rejected_ShowsErrorCode()
        {
            using var db = NewDb();
            var seeded = SeedTask(db, 1, TesState.QUEUEDEnum);

            var result = await ControllerFor(db, "tre-a-admin").ReportResult(new FederatedTaskResultReport
            {
                TaskId = seeded.TaskId.ToString(),
                State = TesState.EXECUTORERROREnum,
                ErrorCode = FederatedTaskErrorCodes.DatasetNotFound,
                ErrorMessage = "no such dataset"
            });

            Assert.IsType<OkObjectResult>(result);
            var task = await GetTask(db, seeded.TaskId);
            Assert.Equal("dataset_not_found", task.Logs![0].Metadata["error_code"]);
        }

        [Fact]
        public async Task ReportResult_OtherTresTask_Returns404_AndUnchanged()
        {
            using var db = NewDb();
            var seeded = SeedTask(db, 1, TesState.QUEUEDEnum);

            var result = await ControllerFor(db, "tre-b-admin").ReportResult(new FederatedTaskResultReport
            {
                TaskId = seeded.TaskId.ToString(),
                State = TesState.COMPLETEEnum,
                Value = 1m,
                RowCount = 1
            });

            Assert.IsType<NotFoundResult>(result);
            Assert.Equal(TesState.QUEUEDEnum, db.FederatedTasks.AsNoTracking().Single().State);
        }

        [Fact]
        public async Task ReportResult_AlreadyReported_Returns409_AndUnchanged()
        {
            using var db = NewDb();
            var seeded = SeedTask(db, 1, TesState.QUEUEDEnum);
            var controller = ControllerFor(db, "tre-a-admin");
            await controller.ReportResult(new FederatedTaskResultReport
            {
                TaskId = seeded.TaskId.ToString(), State = TesState.COMPLETEEnum, Value = 40.5m, RowCount = 2
            });

            var second = await controller.ReportResult(new FederatedTaskResultReport
            {
                TaskId = seeded.TaskId.ToString(), State = TesState.COMPLETEEnum, Value = 99m, RowCount = 3
            });

            Assert.IsType<ConflictResult>(second);
            Assert.Equal(40.5m, db.FederatedTasks.AsNoTracking().Single().ResultValue);
        }

        [Theory]
        [InlineData("not-a-guid", TesState.COMPLETEEnum)]
        [InlineData("00000000-0000-0000-0000-000000000001", TesState.RUNNINGEnum)]
        public async Task ReportResult_BadInput_Returns400(string taskId, TesState state)
        {
            using var db = NewDb();
            var result = await ControllerFor(db, "tre-a-admin").ReportResult(
                new FederatedTaskResultReport { TaskId = taskId, State = state });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task GetTask_Unknown_Returns404()
        {
            using var db = NewDb();
            var result = await ControllerFor(db, "dare-admin").GetTask(Guid.NewGuid().ToString());
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
