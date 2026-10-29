using System.Text;
using Agent.Api.FederatedTasks;
using Xunit;

namespace Agent.Api.Tests.FederatedTasks;

public class CsvDatasetParserTests
{
    [Fact]
    public void Parse_ExcelStyle()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("id,note\n1,\"a, b\"\n\n"))
            .ToArray();

        var result = CsvDatasetParser.Parse(new MemoryStream(bytes));

        Assert.True(result.IsSuccess);
        Assert.Equal(["id", "note"], result.Table!.Columns);
        var row = Assert.Single(result.Table.Rows);
        Assert.Equal(["1", "a, b"], row);
    }

    [Fact]
    public void Parse_EmptyFile_ReturnsDatasetNotFound()
    {
        var result = CsvDatasetParser.Parse(new MemoryStream([]));

        Assert.Equal("dataset_not_found", result.ErrorCode);
        Assert.Equal("dataset file is empty", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ShortRow_PadsWithNull()
    {
        var result = CsvDatasetParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes("a,b,c\n1,2\n")));

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Table!.Rows);
        Assert.Equal(["1", "2", null], row);
    }

    [Fact]
    public void Parse_HeaderOnly_ReturnsEmptyTable()
    {
        var result = CsvDatasetParser.Parse(new MemoryStream(Encoding.UTF8.GetBytes("a,b\n")));

        Assert.True(result.IsSuccess);
        Assert.Equal(["a", "b"], result.Table!.Columns);
        Assert.Empty(result.Table.Rows);
    }
}
