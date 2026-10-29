using Agent.Api.FederatedTasks;
using Agent.Api.Models;
using Xunit;

namespace Agent.Api.Tests.FederatedTasks;

public class DatasetReaderTests
{
    private static DatasetReader Reader(FakeDatasetStore store) => new(store, new FederatedTaskSettings());

    [Fact]
    public async Task Read_ValidCsv_ReturnsTable()
    {
        var store = new FakeDatasetStore().Add("dataset/cohort_2024/cohort.csv", "person_id,age,sex\n1,34,F\n2,51,M\n");

        var result = await Reader(store).ReadAsync("cohort_2024", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["person_id", "age", "sex"], result.Table!.Columns);
        Assert.Equal(2, result.Table.Rows.Count);
        Assert.Equal(["2", "51", "M"], result.Table.Rows[1]);
    }

    [Fact]
    public async Task Read_NoFile_ReturnsDatasetNotFound()
    {
        var result = await Reader(new FakeDatasetStore()).ReadAsync("cohort_2024", CancellationToken.None);

        Assert.Equal("dataset_not_found", result.ErrorCode);
    }

    [Theory]
    [InlineData("dataset/cohort_2024/readme.txt")]
    [InlineData("dataset/cohort_2024/cohort.CSV")]
    public async Task Read_NoMatchingExtension_ReturnsDatasetNotFound(string key)
    {
        var store = new FakeDatasetStore().Add(key, "a,b\n1,2\n");

        var result = await Reader(store).ReadAsync("cohort_2024", CancellationToken.None);

        Assert.Equal("dataset_not_found", result.ErrorCode);
    }

    [Fact]
    public async Task Read_TwoCsvFiles_ReturnsAmbiguousDataset()
    {
        var store = new FakeDatasetStore()
            .Add("dataset/cohort_2024/a.csv", "a\n1\n")
            .Add("dataset/cohort_2024/b.csv", "a\n2\n");

        var result = await Reader(store).ReadAsync("cohort_2024", CancellationToken.None);

        Assert.Equal("ambiguous_dataset", result.ErrorCode);
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("Cohort")]
    [InlineData("")]
    public async Task Read_InvalidName_ReturnsInvalidDatasetName_AndDoesNotList(string name)
    {
        var store = new FakeDatasetStore();

        var result = await Reader(store).ReadAsync(name, CancellationToken.None);

        Assert.Equal("invalid_dataset_name", result.ErrorCode);
        Assert.Equal(0, store.ListCalls);
    }

    [Fact]
    public async Task Read_DoesNotMatchSiblingWithSamePrefix()
    {
        var store = new FakeDatasetStore()
            .Add("dataset/cohort/a.csv", "x\n1\n")
            .Add("dataset/cohort_2024/b.csv", "y\n2\n");

        var result = await Reader(store).ReadAsync("cohort", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["x"], result.Table!.Columns);
    }
}
