using System.Text.Json;
using Agent.Api.FederatedTasks;
using Xunit;

namespace Agent.Api.Tests.FederatedTasks;

public class DatasetUploadPolicyTests
{
    private static Dictionary<string, JsonElement> StatementsBySid(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("Statement")
            .EnumerateArray()
            .ToDictionary(s => s.GetProperty("Sid").GetString()!, s => s.Clone());
    }

    private static string[] Strings(JsonElement statement, string property) =>
        statement.GetProperty(property).EnumerateArray().Select(e => e.GetString()!).ToArray();

    [Fact]
    public void Build_AllowsUploadOnlyForGivenExtensionsUnderPrefix()
    {
        var statements = StatementsBySid(DatasetUploadPolicy.Build("federated-datasets", "dataset/", ["csv"]));

        var upload = statements["UploadDatasets"];
        Assert.Equal(
            ["s3:PutObject", "s3:AbortMultipartUpload", "s3:ListMultipartUploadParts"],
            Strings(upload, "Action"));
        Assert.Equal(["arn:aws:s3:::federated-datasets/dataset/*.csv"], Strings(upload, "Resource"));
    }

    [Fact]
    public void Build_ReadIsLimitedToPrefix()
    {
        var statements = StatementsBySid(DatasetUploadPolicy.Build("federated-datasets", "dataset/", ["csv"]));

        var read = statements["ReadDatasets"];
        Assert.Equal(["s3:GetObject"], Strings(read, "Action"));
        Assert.Equal(["arn:aws:s3:::federated-datasets/dataset/*"], Strings(read, "Resource"));
    }

    [Fact]
    public void Build_ConsoleCanListBuckets()
    {
        var statements = StatementsBySid(DatasetUploadPolicy.Build("federated-datasets", "dataset/", ["csv"]));

        var seeBuckets = statements["SeeBuckets"];
        Assert.Equal(
            ["s3:ListAllMyBuckets", "s3:GetBucketLocation", "s3:GetBucketPolicyStatus"],
            Strings(seeBuckets, "Action"));
        Assert.Equal(["arn:aws:s3:::*"], Strings(seeBuckets, "Resource"));

        var list = statements["ListDatasets"];
        Assert.Equal(["s3:ListBucket", "s3:ListBucketMultipartUploads"], Strings(list, "Action"));
        Assert.Equal(["arn:aws:s3:::federated-datasets"], Strings(list, "Resource"));
    }

    [Fact]
    public void Build_GrantsNoDelete()
    {
        var statements = StatementsBySid(DatasetUploadPolicy.Build("federated-datasets", "dataset/", ["csv"]));

        var actions = statements.Values.SelectMany(s => Strings(s, "Action"));
        Assert.DoesNotContain(actions, a => a.StartsWith("s3:Delete", StringComparison.OrdinalIgnoreCase));
    }
}
