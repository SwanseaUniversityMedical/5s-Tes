using System.Text.Json;

namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// Builds the RustFS canned policy that lets a TRE admin upload federated task datasets
    /// (and nothing else) through the object store console.
    /// </summary>
    public static class DatasetUploadPolicy
    {
        /// <summary>
        /// Policy JSON: list/read access scoped to the dataset bucket and prefix, and upload
        /// restricted to objects with one of the allowed extensions under the prefix. No delete.
        /// </summary>
        public static string Build(string bucket, string prefix, IEnumerable<string> extensions)
        {
            var policy = new
            {
                Version = "2012-10-17",
                Statement = new object[]
                {
                    new
                    {
                        Sid = "SeeBuckets",
                        Effect = "Allow",
                        Action = new[] { "s3:ListAllMyBuckets", "s3:GetBucketLocation", "s3:GetBucketPolicyStatus" },
                        Resource = new[] { "arn:aws:s3:::*" }
                    },
                    new
                    {
                        Sid = "ListDatasets",
                        Effect = "Allow",
                        Action = new[] { "s3:ListBucket", "s3:ListBucketMultipartUploads" },
                        Resource = new[] { $"arn:aws:s3:::{bucket}" }
                    },
                    new
                    {
                        Sid = "ReadDatasets",
                        Effect = "Allow",
                        Action = new[] { "s3:GetObject" },
                        Resource = new[] { $"arn:aws:s3:::{bucket}/{prefix}*" }
                    },
                    new
                    {
                        Sid = "UploadDatasets",
                        Effect = "Allow",
                        Action = new[] { "s3:PutObject", "s3:AbortMultipartUpload", "s3:ListMultipartUploadParts" },
                        Resource = extensions.Select(e => $"arn:aws:s3:::{bucket}/{prefix}*.{e}").ToArray()
                    }
                }
            };

            return JsonSerializer.Serialize(policy);
        }
    }
}
