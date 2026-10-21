using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Aws4RequestSigner;
using Microsoft.AspNetCore.Http;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Net;
using System.Text;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;

namespace FiveSafesTes.Core.Services
{
    public class MinioHelper : IMinioHelper
    {
        private readonly MinioSettings _minioSettings;

        public MinioHelper(MinioSettings minioSettings)
        {
            _minioSettings = minioSettings;
        }

        public async Task<bool> CheckBucketExists(string bucketName = "")
        {
            try
            {
                if (string.IsNullOrEmpty(bucketName))
                {
                    bucketName = _minioSettings.BucketName;
                }

                var amazonS3Client = GenerateAmazonS3Client();

                var buckets = await amazonS3Client.ListBucketsAsync();
                return buckets.Buckets.Any(x => x.BucketName.Equals(bucketName));
            }
            catch (Exception e)
            {
                Log.Error(e, "{Function} Something went wrong", "CheckBucketExists");
                throw;
            }
        }


        public async Task<ListObjectsV2Response> GetFilesInBucket(string bucketName, string prefix = "")
        {
            try
            {
                ListObjectsV2Request request = new ListObjectsV2Request
                {
                    BucketName = bucketName,
                    Prefix = prefix
                };

                var amazonS3Client = GenerateAmazonS3Client();
                var data = await amazonS3Client.ListObjectsV2Async(request);
                Log.Information("{bucketName} created successfully.", bucketName);
                return data;
            }
            catch (MinioException e)
            {
                Log.Warning("GetFilesInBucket: {bucketName}, failed due to Minio exception: {message}", bucketName,
                    e.Message);
            }
            catch (Exception ex)
            {
                Log.Warning("GetFilesInBucket: {bucketName}, failed due to Exception: {message}", bucketName,
                    ex.Message);
            }

            return null;
        }

        public async Task<bool> CreateBucket(string bucketName = "")
        {
            if (string.IsNullOrEmpty(bucketName))
            {
                bucketName = _minioSettings.BucketName;
            }

            if (!string.IsNullOrEmpty(bucketName))
            {
                try
                {
                    // Create bucket if it doesn't exist.
                    bool found = await CheckBucketExists(bucketName);

                    if (found)
                    {
                        Log.Information("{bucketName} already exists.", bucketName);
                        return true;
                    }
                    else
                    {
                        var amazonS3Client = GenerateAmazonS3Client();
                        await amazonS3Client.PutBucketAsync(bucketName);
                        Log.Information("{bucketName} created successfully.", bucketName);
                        return true;
                    }
                }
                catch (MinioException e)
                {
                    Log.Warning("Create bucket: {bucketName}, failed due to Minio exception: {message}", bucketName,
                        e.Message);
                }
                catch (Exception ex)
                {
                    Log.Warning("Create bucket: {bucketName}, failed due to Exception: {message}", bucketName,
                        ex.Message);
                }
            }
            else
            {
                Log.Warning("Cannot create bucket as bucket name is null or empty.");
            }

            return false;
        }

        public async Task<bool> UploadFileAsync(IFormFile? filePath, string bucketName = "", string objectName = "")
        {
            try
            {
                var amazonS3Client = GenerateAmazonS3Client();
                if (filePath != null)
                {
                    using (var stream = new MemoryStream())
                    {
                        await filePath.CopyToAsync(stream);

                        var uploadRequest = new PutObjectRequest
                        {
                            BucketName = bucketName,
                            Key = filePath.FileName,
                            InputStream = stream, // outstream,
                            ContentType = filePath.ContentType
                        };


                        var response = await amazonS3Client.PutObjectAsync(uploadRequest);
                        if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                        {
                            Log.Warning($"Successfully uploaded {objectName} to {bucketName}.");
                            return true;
                        }
                        else
                        {
                            Log.Warning($"Could not upload {objectName} to {bucketName}.");
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Warning("UploadFile: {filePath}, failed: {message}", filePath, e.Message);
                throw;
            }
        }


        public async Task WriteToStore(string bucketName, string filePath, MemoryStream file)
        {
            var amazonS3Client = GenerateAmazonS3Client();
            var objectName = Path.GetFileName(filePath);
            var putObjectRequest = new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = objectName,
                InputStream = file,
                ContentType = "text/plain"
            };
            Log.Information("Uploading '{TargetObject} to {Bucket}...", filePath, bucketName);
            await amazonS3Client.PutObjectAsync(putObjectRequest);
            Log.Debug("Successfully uploaded {TargetObject} to {Bucket}", filePath, bucketName);
        }

        public async Task<bool> DownloadFileAsync(string bucketName = "", string objectName = "")
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = objectName,
            };

            var amazonS3Client = GenerateAmazonS3Client();

            var objectExists = await CheckObjectExists(request.BucketName, request.Key);

            if (objectExists)
            {
                var response = await amazonS3Client.GetObjectAsync(request);

                using (var responseStream = response.ResponseStream)
                {
                    string saveFilePath = "C:/Path/To/Save/File.txt";

                    using (var fileStream = new FileStream(saveFilePath, FileMode.Create))
                    {
                        await responseStream.CopyToAsync(fileStream);
                    }
                }

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    Log.Warning($"Successfully uploaded {objectName} to {bucketName}.");
                    return true;
                }
                else
                {
                    Log.Warning($"Could not upload {objectName} to {bucketName}.");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task DeleteObject(string bucketName, string objectKey)
        {
            var amazonS3Client = GenerateAmazonS3Client();

            try
            {
                await amazonS3Client.DeleteObjectAsync(bucketName, objectKey);
            }
            catch (AmazonS3Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        /// <summary>
        /// Removes every object from a bucket (paginated list + bulk delete) without removing the
        /// bucket itself. S3/MinIO requires a bucket to be empty before it can be deleted, so this is
        /// also the first step of <see cref="DeleteBucketAsync"/>.
        /// </summary>
        public async Task<bool> EmptyBucketAsync(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                return false;
            }

            var amazonS3Client = GenerateAmazonS3Client();

            try
            {
                string? continuationToken = null;
                do
                {
                    var listRequest = new ListObjectsV2Request
                    {
                        BucketName = bucketName,
                        ContinuationToken = continuationToken
                    };

                    var listResponse = await amazonS3Client.ListObjectsV2Async(listRequest);

                    if (listResponse.S3Objects.Count > 0)
                    {
                        var deleteRequest = new DeleteObjectsRequest
                        {
                            BucketName = bucketName,
                            Objects = listResponse.S3Objects.Select(o => new KeyVersion { Key = o.Key }).ToList()
                        };

                        await amazonS3Client.DeleteObjectsAsync(deleteRequest);
                    }

                    continuationToken = listResponse.IsTruncated ? listResponse.NextContinuationToken : null;
                }
                while (continuationToken != null);

                Log.Information("Emptied bucket {BucketName}", bucketName);
                return true;
            }
            catch (AmazonS3Exception ex)
            {
                Log.Error(ex, "Failed to empty bucket {BucketName}", bucketName);
                return false;
            }
        }

        /// <summary>
        /// Deletes a bucket entirely: empties it first (S3 requires an empty bucket) then removes it.
        /// If the bucket does not exist, the method returns true because there is nothing to delete. so cleanup is idempotent.
        /// </summary>
        public async Task<bool> DeleteBucketAsync(string bucketName)
        {
            if (string.IsNullOrWhiteSpace(bucketName))
            {
                return false;
            }

            if (!await CheckBucketExists(bucketName))
            {
                Log.Information("Bucket {BucketName} does not exist; nothing to delete", bucketName);
                return true;
            }

            if (!await EmptyBucketAsync(bucketName))
            {
                return false;
            }

            var amazonS3Client = GenerateAmazonS3Client();

            try
            {
                await amazonS3Client.DeleteBucketAsync(bucketName);
                Log.Information("Deleted bucket {BucketName}", bucketName);
                return true;
            }
            catch (AmazonS3Exception ex)
            {
                Log.Error(ex, "Failed to delete bucket {BucketName}", bucketName);
                return false;
            }
        }

        public async Task<bool> CheckObjectExists(string bucketName, string objectKey)
        {
            var request = new GetObjectMetadataRequest
            {
                BucketName = bucketName,
                Key = objectKey
            };

            var amazonS3Client = GenerateAmazonS3Client();

            try
            {
                await amazonS3Client.GetObjectMetadataAsync(request);
                Log.Warning($"{request.Key} Exists on {bucketName}.");
                return true;
            }
            catch (AmazonS3Exception ex)
            {
                if (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Log.Warning($"{request.Key} Not Exists on {bucketName}.");
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        public static string StreamToString(Stream stream)
        {
            stream.Position = 0;
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }


        public async Task<bool> FetchAndStoreObject(string url, string bucketName, string key)
        {
            try
            {
                HttpClientHandler handler;
                if (!string.IsNullOrWhiteSpace(_minioSettings.ProxyAddresURLForExternalFetch))
                {
                    var proxy = new System.Net.WebProxy(_minioSettings.ProxyAddresURLForExternalFetch);
                    handler = new HttpClientHandler
                    {
                        Proxy = proxy,
                        UseProxy = true
                    };
                    Log.Information("{Function} Using proxy {Proxy}", "FetchAndStoreObject",
                        _minioSettings.ProxyAddresURLForExternalFetch);
                }
                else
                {
                    handler = new HttpClientHandler
                    {
                        UseProxy = false
                    };
                    Log.Information("{Function} Not Using proxy", "FetchAndStoreObject");
                }


                // Configure the HttpClientHandler to use the proxy


                // Create the HttpClient with the handler
                using (var httpClient = new HttpClient(handler))
                {
                    var response = await httpClient.GetAsync(url);


                    response.EnsureSuccessStatusCode();

                    var contentBytes = await response.Content.ReadAsByteArrayAsync();

                    var amazonS3Client = GenerateAmazonS3Client();

                    using (var transferUtility = new TransferUtility(amazonS3Client))
                    {
                        await transferUtility.UploadAsync(new MemoryStream(contentBytes), bucketName, key);
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Error(e, "{Function} It went wrong", "FetchAndStoreObject");
                throw;
            }
        }

        public async Task<bool> RabbitExternalObject(MQFetchFile msgBytes)
        {
            if (msgBytes == null)
            {
                Log.Information("{Function} Empty message", "RabbitExternalObject");
                return false;
            }
            else
            {
                Log.Information("{Function} Fetching", "RabbitExternalObject");
                await FetchAndStoreObject(msgBytes.Url, msgBytes.BucketName, msgBytes.Key);
                Log.Information("{Function} Fetched", "RabbitExternalObject");
                return true;
            }
        }

        public async Task<bool> RabbitExternalObject(string msgBytes)
        {
            var FileInfo = JsonConvert.DeserializeObject<MQFetchFile>(msgBytes);
            if (FileInfo == null)
            {
                return false;
            }
            else
            {
                await FetchAndStoreObject(FileInfo.Url, FileInfo.BucketName, FileInfo.Key);
                return true;
            }
        }

        /// <summary>
        /// No-op on Ceph RGW. Under RustFS/MinIO this created a detached "{bucket}_policy" canned
        /// policy that nothing ever attached to a user. Ceph RGW has no canned-policy objects:
        /// access is granted by Principal-scoped statements on the bucket policy itself, which
        /// <see cref="CreateProjectS3AccessPolicyAsync"/> adds once the access key is known.
        /// Kept so existing bucket-creation call sites keep working.
        /// </summary>
        public Task<bool> CreateBucketPolicy(string bucketName)
        {
            Log.Debug("CreateBucketPolicy is a no-op on Ceph RGW (bucket {BucketName})", bucketName);
            return Task.FromResult(true);
        }


        public Task<bool> BucketPolicySetPublic(string bucketName)
        {
            return SetPublicPolicy(bucketName);
        }

        public async Task<bool> CopyObjectToDestination(string destinationBucketName, string destinationObjectKey,
            GetObjectResponse response)
        {
            var amazonS3Client = GenerateAmazonS3Client();


            long contentLength = response.Headers.ContentLength;
            using (Stream responseStream = response.ResponseStream)
            {
                using (MemoryStream memoryStream = new MemoryStream())
                {
                    await responseStream.CopyToAsync(memoryStream);

                    PutObjectRequest putObjectRequest = new PutObjectRequest
                    {
                        BucketName = destinationBucketName,
                        Key = destinationObjectKey,
                        InputStream = memoryStream,
                        ContentType = response.Headers.ContentType,
                        
                    };

                    var putObjectResponse = await amazonS3Client.PutObjectAsync(putObjectRequest);


                    return putObjectResponse.HttpStatusCode == HttpStatusCode.OK;
                }
            }
        }

        public async Task<GetObjectResponse> GetCopyObject(string sourceBucketName, string sourceObjectKey)
        {
            var amazonS3Client = GenerateAmazonS3Client();


            GetObjectRequest getObjectRequest = new GetObjectRequest
            {
                BucketName = sourceBucketName,
                Key = sourceObjectKey
            };

            var getObjectResponse = await amazonS3Client.GetObjectAsync(getObjectRequest);

            return getObjectResponse;
        }

        public async Task<string> ShareMinioObject(string bucketName, string objectKey)
        {
            var amazonS3Client = GenerateAmazonS3Client();

            var expiration = DateTime.Now.AddHours(1);


            var url = amazonS3Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                Expires = expiration,
            });

            return url;
        }

        public async Task<bool> FolderExists(string bucketName, string folderName)
        {
            var amazonS3Client = GenerateAmazonS3Client();
            try
            {
                var listReqest = new ListObjectsRequest
                {
                    BucketName = bucketName,
                    Prefix = folderName
                };

                var listResponse = await amazonS3Client.ListObjectsAsync(listReqest);
                if (!listResponse.S3Objects.Any())
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                return true;
            }
        }

        public async Task<bool> CreateFolder(string bucketName, string folderName)
        {
            var amazonS3Client = GenerateAmazonS3Client();
            try
            {
                var putRequest = new PutObjectRequest
                {
                    BucketName = bucketName,
                    Key = folderName + "/",
                    ContentBody = string.Empty
                };

                var putResponse = await amazonS3Client.PutObjectAsync(putRequest);

                if (putResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> SetPublicPolicy(string bucketName)
        {
            var amazonS3Client = GenerateAmazonS3Client();

            string bucketPolicyJson = $@"
{{
    ""Version"": ""2012-10-17"",
    ""Statement"": [
        {{
            ""Effect"": ""Allow"",
            ""Principal"": {{
                ""AWS"": [
                    ""*""
                ]
            }},
            ""Action"": [
                ""s3:GetBucketLocation"",
                ""s3:ListBucket"",
                ""s3:ListBucketMultipartUploads""
            ],
            ""Resource"": [
                ""arn:aws:s3:::{bucketName}""
            ]
        }},
        {{
            ""Effect"": ""Allow"",
            ""Principal"": {{
                ""AWS"": [
                    ""*""
                ]
            }},
            ""Action"": [
                ""s3:PutObject"",
                ""s3:AbortMultipartUpload"",
                ""s3:DeleteObject"",
                ""s3:GetObject"",
                ""s3:ListMultipartUploadParts""
            ],
            ""Resource"": [
                ""arn:aws:s3:::{bucketName}/*""
            ]
        }}
    ]
}}";
            try
            {
                // Set the bucket policy
                PutBucketPolicyRequest putBucketPolicyRequest = new PutBucketPolicyRequest
                {
                    BucketName = bucketName,
                    Policy = bucketPolicyJson
                };

                amazonS3Client.PutBucketPolicyAsync(putBucketPolicyRequest).Wait();

                Log.Information("Bucket policy set successfully!");
            }
            catch (Exception e)
            {
                Log.Error($"Error: {e.Message}");
            }

            return true;
        }


        #region PrivateHelpers

        private AmazonS3Config GenerateAmazonS3Config()
        {
            var config = new AmazonS3Config
            {
                RegionEndpoint =
                    RegionEndpoint
                        .USEast1, // MUST set this before setting ServiceURL and it should match the `MINIO_REGION` environment variable.
                ServiceURL = _minioSettings.Url, // replace http://localhost:9000 with URL of your MinIO server
                ForcePathStyle = true, // MUST be true to work correctly with MinIO server
            };

            if (_minioSettings.UesProxy)
            {
                config.SetWebProxy(new System.Net.WebProxy
                {
                    Address = new Uri(_minioSettings.ProxyAddresURL),
                    BypassList = new[] { _minioSettings.BypassProxy }
                });
            }


            return config;
        }

        private AmazonS3Client GenerateAmazonS3Client()
        {
            var config = GenerateAmazonS3Config();
            return new AmazonS3Client(_minioSettings.AccessKey, _minioSettings.SecretKey, config);
        }

        private long CalcularePartSize(long objectSize, long partSize)
        {
            var numParts = objectSize / partSize;
            //s3 can only handle 10000 parts so if to large then increase size of part
            if (numParts >= 10000)
            {
                partSize *= 2;
                return CalcularePartSize(objectSize, partSize);
            }
            else
            {
                return partSize;
            }
        }

        #endregion

        #region Secret Management Methods

        /// <summary>
        /// Create an S3 user (access key/secret key pair) via the Ceph RGW Admin Ops API.
        /// The access key doubles as the RGW uid so bucket-policy Principals can reference it.
        /// </summary>
        /// <param name="accessKey">The access key name (also used as the RGW uid)</param>
        /// <param name="secretKey">The secret key value (optional, RGW generates one if not provided)</param>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult with the RGW response body in Output</returns>
        public async Task<MinioCommandResult> CreateMinioSecretAsync(string accessKey, string secretKey = "", CancellationToken cancellationToken = default)
        {
            try
            {
                var query = new Dictionary<string, string>
                {
                    ["uid"] = accessKey,
                    ["display-name"] = accessKey,
                    ["key-type"] = "s3",
                    ["access-key"] = accessKey,
                };

                if (string.IsNullOrEmpty(secretKey))
                {
                    // Let RGW generate the secret key.
                    query["generate-key"] = "true";
                }
                else
                {
                    query["secret-key"] = secretKey;
                    query["generate-key"] = "false";
                }

                var result = await SendRgwAdminRequestAsync(HttpMethod.Put, "user", query, cancellationToken);

                if (result.Success)
                {
                    Log.Information("S3 user created successfully for access key: {AccessKey}", accessKey);
                }
                else
                {
                    Log.Warning("Failed to create S3 user for access key: {AccessKey}. Error: {Error}", accessKey, result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while creating S3 user for access key: {AccessKey}", accessKey);
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Delete an S3 user (and its keys) via the Ceph RGW Admin Ops API.
        /// </summary>
        /// <param name="accessKey">The access key (RGW uid) to delete</param>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult indicating success or failure</returns>
        public async Task<MinioCommandResult> DeleteMinioSecretAsync(string accessKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = new Dictionary<string, string> { ["uid"] = accessKey };
                var result = await SendRgwAdminRequestAsync(HttpMethod.Delete, "user", query, cancellationToken);

                if (result.Success)
                {
                    Log.Information("S3 user deleted successfully for access key: {AccessKey}", accessKey);
                }
                else
                {
                    Log.Warning("Failed to delete S3 user for access key: {AccessKey}. Error: {Error}", accessKey, result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while deleting S3 user for access key: {AccessKey}", accessKey);
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// List all S3 users (RGW uids) via the Ceph RGW metadata API.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult with a JSON array of uids in Output</returns>
        public async Task<MinioCommandResult> ListMinioSecretsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await SendRgwAdminRequestAsync(HttpMethod.Get, "metadata/user", null, cancellationToken);

                if (result.Success)
                {
                    Log.Information("S3 users listed successfully");
                }
                else
                {
                    Log.Warning("Failed to list S3 users. Error: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while listing S3 users");
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Get information about a specific S3 user via the Ceph RGW Admin Ops API.
        /// Fails (Success = false, ExitCode = 404) when the user does not exist.
        /// </summary>
        /// <param name="accessKey">The access key (RGW uid) to get information about</param>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult with the RGW user JSON in Output</returns>
        public async Task<MinioCommandResult> GetMinioSecretAsync(string accessKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var query = new Dictionary<string, string> { ["uid"] = accessKey };
                var result = await SendRgwAdminRequestAsync(HttpMethod.Get, "user", query, cancellationToken);

                if (result.Success)
                {
                    Log.Information("S3 user information retrieved successfully for access key: {AccessKey}", accessKey);
                }
                else
                {
                    Log.Warning("Failed to get S3 user information for access key: {AccessKey}. Error: {Error}", accessKey, result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while getting S3 user information for access key: {AccessKey}", accessKey);
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Grants an S3 user read on the project's submission bucket and read/write/delete on its
        /// output bucket. Ceph RGW has no detached canned policies, so this merges
        /// Principal-scoped statements (Sids tagged with <paramref name="policyName"/>) into each
        /// bucket's existing policy. Idempotent: statements with the same Sids are replaced.
        /// </summary>
        public async Task<bool> CreateProjectS3AccessPolicyAsync(
            string policyName,
            string accessKey,
            string submissionBucket,
            string outputBucket)
        {
            if (string.IsNullOrWhiteSpace(policyName) || string.IsNullOrWhiteSpace(accessKey) ||
                string.IsNullOrWhiteSpace(submissionBucket) || string.IsNullOrWhiteSpace(outputBucket))
            {
                return false;
            }

            var principal = new JObject { ["AWS"] = $"arn:aws:iam:::user/{accessKey}" };
            var sidPrefix = BuildPolicySidPrefix(policyName);

            JObject Statement(string sidSuffix, string[] actions, string resource) => new JObject
            {
                ["Sid"] = sidPrefix + sidSuffix,
                ["Effect"] = "Allow",
                ["Principal"] = principal.DeepClone(),
                ["Action"] = new JArray(actions),
                ["Resource"] = resource
            };

            // A bucket policy may only reference its own bucket, so group statements per bucket.
            // When submission and output are the same bucket, all three land on it.
            var statementsByBucket = new Dictionary<string, List<JObject>>();
            void Add(string bucket, JObject statement)
            {
                if (!statementsByBucket.TryGetValue(bucket, out var list))
                {
                    statementsByBucket[bucket] = list = new List<JObject>();
                }
                list.Add(statement);
            }

            Add(submissionBucket, Statement("ListSubmission",
                new[] { "s3:ListBucket", "s3:GetBucketLocation" }, $"arn:aws:s3:::{submissionBucket}"));
            Add(submissionBucket, Statement("ReadSubmission",
                new[] { "s3:GetObject" }, $"arn:aws:s3:::{submissionBucket}/*"));
            Add(outputBucket, Statement("ListOutput",
                new[] { "s3:ListBucket", "s3:GetBucketLocation" }, $"arn:aws:s3:::{outputBucket}"));
            Add(outputBucket, Statement("WriteOutput",
                new[] { "s3:PutObject", "s3:GetObject", "s3:DeleteObject" }, $"arn:aws:s3:::{outputBucket}/*"));

            try
            {
                var amazonS3Client = GenerateAmazonS3Client();

                foreach (var (bucket, statements) in statementsByBucket)
                {
                    var policy = await GetBucketPolicyDocumentAsync(amazonS3Client, bucket);
                    var merged = StripPolicyStatements(policy, sidPrefix);
                    foreach (var statement in statements)
                    {
                        merged.Add(statement);
                    }

                    policy["Statement"] = merged;
                    await amazonS3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
                    {
                        BucketName = bucket,
                        Policy = policy.ToString(Formatting.None)
                    });
                }

                Log.Information(
                    "Applied project S3 access policy {PolicyName} for {AccessKey} on buckets {SubmissionBucket}/{OutputBucket}",
                    policyName, accessKey, submissionBucket, outputBucket);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex,
                    "CreateProjectS3AccessPolicyAsync failed for {PolicyName} on buckets {SubmissionBucket}/{OutputBucket}",
                    policyName, submissionBucket, outputBucket);
                return false;
            }
        }

        /// <summary>
        /// Returns true when an S3 access key (user) already exists on the object store.
        /// Wraps <see cref="GetMinioSecretAsync"/> (GET /admin/user), which succeeds only
        /// when the user is present.
        /// </summary>
        public async Task<bool> UserExistsAsync(string accessKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(accessKey))
            {
                return false;
            }

            var result = await GetMinioSecretAsync(accessKey, cancellationToken);
            return result.Success;
        }

        /// <summary>
        /// Strips the statements tagged with <paramref name="policyName"/> from the submission and
        /// output bucket policies (cleanup for an access key's grants when it is revoked). The
        /// bucket policy is deleted outright when nothing else remains in it. Idempotent: a bucket
        /// with no policy, or no matching statements, counts as success.
        /// </summary>
        public async Task<bool> RemovePolicyAsync(
            string policyName,
            string submissionBucket,
            string outputBucket,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(policyName))
            {
                return false;
            }

            var sidPrefix = BuildPolicySidPrefix(policyName);
            var buckets = new[] { submissionBucket, outputBucket }
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct()
                .ToList();

            if (buckets.Count == 0)
            {
                return false;
            }

            var amazonS3Client = GenerateAmazonS3Client();
            var allRemoved = true;

            foreach (var bucket in buckets)
            {
                try
                {
                    var policy = await GetBucketPolicyDocumentAsync(amazonS3Client, bucket, cancellationToken);
                    var existing = NormaliseStatements(policy);
                    var remaining = StripPolicyStatements(policy, sidPrefix);

                    if (remaining.Count == existing.Count)
                    {
                        continue;
                    }

                    if (remaining.Count == 0)
                    {
                        await amazonS3Client.DeleteBucketPolicyAsync(bucket, cancellationToken);
                    }
                    else
                    {
                        policy["Statement"] = remaining;
                        await amazonS3Client.PutBucketPolicyAsync(new PutBucketPolicyRequest
                        {
                            BucketName = bucket,
                            Policy = policy.ToString(Formatting.None)
                        }, cancellationToken);
                    }

                    Log.Information("Removed S3 policy {PolicyName} statements from bucket {BucketName}", policyName, bucket);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception removing S3 policy {PolicyName} from bucket {BucketName}", policyName, bucket);
                    allRemoved = false;
                }
            }

            return allRemoved;
        }

        #endregion

        #region Ceph RGW Helper Methods

        /// <summary>
        /// Sid prefix identifying the statements a named policy owns inside a bucket policy.
        /// Restricted to alphanumerics, the portable Sid character set.
        /// </summary>
        private static string BuildPolicySidPrefix(string policyName) =>
            new string(policyName.Where(char.IsLetterOrDigit).ToArray());

        private static readonly string[] PolicySidSuffixes =
            { "ListSubmission", "ReadSubmission", "ListOutput", "WriteOutput" };

        private static JArray NormaliseStatements(JObject policy) =>
            policy["Statement"] switch
            {
                JArray array => array,
                JObject single => new JArray(single),
                _ => new JArray()
            };

        /// <summary>
        /// Returns the policy's statements minus those owned by <paramref name="sidPrefix"/>.
        /// </summary>
        private static JArray StripPolicyStatements(JObject policy, string sidPrefix)
        {
            var ownedSids = PolicySidSuffixes.Select(s => sidPrefix + s).ToHashSet();
            return new JArray(NormaliseStatements(policy)
                .Where(s => !ownedSids.Contains(s["Sid"]?.ToString() ?? string.Empty)));
        }

        /// <summary>
        /// Reads a bucket's policy as JSON, or an empty policy document when it has none.
        /// </summary>
        private static async Task<JObject> GetBucketPolicyDocumentAsync(
            AmazonS3Client amazonS3Client,
            string bucketName,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await amazonS3Client.GetBucketPolicyAsync(bucketName, cancellationToken);
                if (!string.IsNullOrWhiteSpace(response.Policy))
                {
                    return JObject.Parse(response.Policy);
                }
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucketPolicy" ||
                                               ex.StatusCode == HttpStatusCode.NotFound)
            {
                // No policy yet.
            }

            return new JObject
            {
                ["Version"] = "2012-10-17",
                ["Statement"] = new JArray()
            };
        }

        /// <summary>
        /// Sends an AWS4-signed request to the Ceph RGW Admin Ops API at {Url}/admin/{resource}.
        /// The configured access key must belong to an RGW user with the matching admin caps
        /// (users=*, metadata=read).
        /// </summary>
        private async Task<MinioCommandResult> SendRgwAdminRequestAsync(
            HttpMethod method,
            string resource,
            IDictionary<string, string>? query,
            CancellationToken cancellationToken = default)
        {
            var signer = new AWS4RequestSigner(_minioSettings.AccessKey, _minioSettings.SecretKey);

            var queryParams = new Dictionary<string, string>(query ?? new Dictionary<string, string>())
            {
                ["format"] = "json"
            };
            var queryString = string.Join("&", queryParams.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

            var baseUrl = _minioSettings.Url.TrimEnd('/');
            using var client = new HttpClient();
            using var request = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new Uri($"{baseUrl}/admin/{resource}?{queryString}")
            };

            var signedRequest = await signer.Sign(request, _minioSettings.AWSService, _minioSettings.AWSRegion);
            using var response = await client.SendAsync(signedRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            return new MinioCommandResult
            {
                Success = response.IsSuccessStatusCode,
                ExitCode = (int)response.StatusCode,
                Output = response.IsSuccessStatusCode ? body : string.Empty,
                Error = response.IsSuccessStatusCode ? string.Empty : $"{(int)response.StatusCode} {body}"
            };
        }

        #endregion
    }
}
