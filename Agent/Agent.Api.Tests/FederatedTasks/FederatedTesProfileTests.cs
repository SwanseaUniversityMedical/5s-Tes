using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;
using Xunit;

namespace Agent.Api.Tests.FederatedTasks;

public class FederatedTesProfileTests
{
    private static TesTask ValidTask() => new()
    {
        Executors =
        [
            new TesExecutor
            {
                Image = "5s-tes/federated-ops:1",
                Command = ["mean", "--column", "age", "--filter", "sex:eq:F"]
            }
        ],
        Inputs = [new TesInput { Url = "dataset://cohort_2024", Path = "/data/cohort_2024", Type = TesFileType.DIRECTORYEnum }],
        Tags = new Dictionary<string, string> { ["tres"] = "tre-a", ["study_id"] = "s1" }
    };

    [Fact]
    public void Parse_Valid()
    {
        var result = FederatedTesProfile.Parse(ValidTask());

        Assert.True(result.IsSuccess);
        var spec = result.Spec!;
        Assert.Equal("mean", spec.Operation);
        Assert.Equal("cohort_2024", spec.Dataset);
        Assert.Equal("age", spec.Column);
        Assert.Equal([new TaskFilter("sex", "eq", "F")], spec.Filters);
        Assert.Equal("s1", spec.StudyId);
        Assert.Equal("tre-a", spec.TreName);
    }

    [Fact]
    public void Parse_FilterValueMayContainColon()
    {
        var task = ValidTask();
        task.Executors[0].Command = ["count", "--filter", "t:eq:10:30"];

        var spec = FederatedTesProfile.Parse(task).Spec!;

        Assert.Equal("10:30", Assert.Single(spec.Filters).Value);
    }

    [Fact]
    public void Parse_CountWithoutColumn_IsValid()
    {
        var task = ValidTask();
        task.Executors[0].Command = ["count"];

        var result = FederatedTesProfile.Parse(task);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Spec!.Column);
    }

    [Fact]
    public void Parse_TreNameIsLowercased()
    {
        var task = ValidTask();
        task.Tags!["TRES"] = "x";
        task.Tags.Remove("tres");
        task.Tags["TRES"] = "TRE-A";

        Assert.Equal("tre-a", FederatedTesProfile.Parse(task).Spec!.TreName);
    }

    [Fact]
    public void Parse_TreNameWithSpaces_IsAccepted()
    {
        var task = ValidTask();
        task.Tags!["tres"] = "Five Safes TRE";

        Assert.Equal("five safes tre", FederatedTesProfile.Parse(task).Spec!.TreName);
    }

    public static TheoryData<string, Action<TesTask>, string> Rejections => new()
    {
        { "0 executors", t => t.Executors = [], FederatedTaskErrorCodes.InvalidTask },
        { "2 executors", t => t.Executors.Add(new TesExecutor { Image = FederatedTesProfile.ExecutorImage, Command = ["count"] }), FederatedTaskErrorCodes.InvalidTask },
        { "wrong image", t => t.Executors[0].Image = "ubuntu:22.04", FederatedTaskErrorCodes.InvalidTask },
        { "0 inputs", t => t.Inputs = [], FederatedTaskErrorCodes.InvalidTask },
        { "s3 input", t => t.Inputs![0].Url = "s3://b/k", FederatedTaskErrorCodes.InvalidTask },
        { "traversal dataset", t => t.Inputs![0].Url = "dataset://../x", FederatedTaskErrorCodes.InvalidDatasetName },
        { "uppercase dataset", t => t.Inputs![0].Url = "dataset://Cohort", FederatedTaskErrorCodes.InvalidDatasetName },
        { "output added", t => t.Outputs = [new TesOutput { Url = "s3://b/o", Path = "/o" }], FederatedTaskErrorCodes.InvalidTask },
        { "multiple tres", t => t.Tags!["tres"] = "tre-a|tre-b", FederatedTaskErrorCodes.InvalidTask },
        { "study_id missing", t => t.Tags!.Remove("study_id"), FederatedTaskErrorCodes.InvalidTask },
        { "unknown operation", t => t.Executors[0].Command = ["median"], FederatedTaskErrorCodes.UnknownOperation },
        { "mean without column", t => t.Executors[0].Command = ["mean"], FederatedTaskErrorCodes.InvalidTask },
        { "bad filter operator", t => t.Executors[0].Command = ["count", "--filter", "sex:like:F"], FederatedTaskErrorCodes.InvalidFilter },
        { "filter without parts", t => t.Executors[0].Command = ["count", "--filter", "sex"], FederatedTaskErrorCodes.InvalidFilter },
        { "trailing newline dataset", t => t.Inputs![0].Url = "dataset://cohort\n", FederatedTaskErrorCodes.InvalidDatasetName },
        { "null executor", t => t.Executors = [null!], FederatedTaskErrorCodes.InvalidTask },
        { "null input", t => t.Inputs = [null!], FederatedTaskErrorCodes.InvalidTask },
        { "null command", t => t.Executors[0].Command = null, FederatedTaskErrorCodes.InvalidTask },
        { "tres blank", t => t.Tags!["tres"] = "  ", FederatedTaskErrorCodes.InvalidTask },
        { "tag keys collide by case", t => t.Tags = new Dictionary<string, string> { ["tres"] = "a", ["TRES"] = "b", ["study_id"] = "s1" }, FederatedTaskErrorCodes.InvalidTask },
        { "unknown arg", t => t.Executors[0].Command = ["count", "--limit", "5"], FederatedTaskErrorCodes.InvalidTask },
    };

    [Theory]
    [MemberData(nameof(Rejections))]
    public void Parse_Rejects(string name, Action<TesTask> change, string expectedCode)
    {
        var task = ValidTask();
        change(task);

        var result = FederatedTesProfile.Parse(task);

        Assert.False(result.IsSuccess, name);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public void Build_RoundTrips()
    {
        var spec = new FederatedTaskSpec(
            "mean", "cohort_2024", "age",
            [new TaskFilter("sex", "eq", "F"), new TaskFilter("age", "gte", "18")],
            "s1", "tre-a");

        var parsed = FederatedTesProfile.Parse(FederatedTesProfile.Build(spec)).Spec!;

        Assert.Equal(spec.Operation, parsed.Operation);
        Assert.Equal(spec.Dataset, parsed.Dataset);
        Assert.Equal(spec.Column, parsed.Column);
        Assert.Equal(spec.StudyId, parsed.StudyId);
        Assert.Equal(spec.TreName, parsed.TreName);
        Assert.True(spec.Filters.SequenceEqual(parsed.Filters));
    }
}
