using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Aws4RequestSigner;
using Microsoft.AspNetCore.Http;
using Minio.DataModel.Args;
using Minio.Exceptions;
using Newtonsoft.Json;
using Serilog;
using System.Diagnostics;
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

        public async Task<bool> CreateBucketPolicy(string bucketName)
        {
            var signer = new AWS4RequestSigner(_minioSettings.AccessKey, _minioSettings.SecretKey);

            var content = new StringContent(
                "{\r\n    \"Version\": \"2012-10-17\",\r\n    \"Statement\": [\r\n        {\r\n            \"Effect\": \"Allow\",\r\n            \"Action\": [\r\n                \"s3:List*\",\r\n                \"s3:ListBucket\",\r\n                \"s3:PutObject\",\r\n                \"s3:DeleteObject\",\r\n                \"s3:GetBucketLocation\",\r\n                \"s3:GetObject\"\r\n            ],\r\n            \"Resource\": [\r\n                \"arn:aws:s3:::" +
                bucketName + "\",\r\n                \"arn:aws:s3:::" + bucketName +
                "/*\"\r\n            ]\r\n        }\r\n    ]\r\n}", null, "application/json");

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(_minioSettings.Url + "/minio/admin/v3/add-canned-policy?name=" + bucketName +
                                     "_policy"),
                Content = content
            };

            request = await signer.Sign(request, _minioSettings.AWSService, _minioSettings.AWSRegion);
            var client = new HttpClient();
            var response = await client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            return true;
        }


        public async Task<bool> BucketPolicySetPublic(string bucketName)
        {
            var signer = new AWS4RequestSigner(_minioSettings.AccessKey, _minioSettings.SecretKey);

            var content = new StringContent(@"{
    ""Version"": ""2012-10-17"",
    ""Statement"": [
        {
            ""Effect"": ""Allow"",
            ""Principal"": ""*"",
            ""Action"": [
                ""s3:GetObject"",
                ""s3:PutObject"",
                ""s3:ListBucket""
            ],
            ""Resource"": [
                ""arn:aws:s3:::" + bucketName + @"/*""
            ]
        }
    ]
}");

            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(_minioSettings.Url + "/minio/admin/v3/add-canned-policy?name=" + bucketName +
                                     "_policy"),
                Content = content
            };

            request = await signer.Sign(request, _minioSettings.AWSService, _minioSettings.AWSRegion);
            var client = new HttpClient();
            var response = await client.SendAsync(request);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                return false;
            }

            return true;
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

        private bool _isMinioClientInitialized = false;

        /// <summary>
        /// Create a MinIO secret (access key/secret key pair)
        /// </summary>
        /// <param name="accessKey">The access key name</param>
        /// <param name="secretKey">The secret key value (optional, will be generated if not provided)</param>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult with the created secret information</returns>
        public async Task<MinioCommandResult> CreateMinioSecretAsync(string accessKey, string secretKey = "", CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureMinioClientInitializedAsync(cancellationToken);

                string command;
                if (string.IsNullOrEmpty(secretKey))
                {
                    // Generate secret key automatically
                    command = $"mc admin user add {_minioSettings.Alias} {accessKey}";
                }
                else
                {
                    command = $"mc admin user add {_minioSettings.Alias} {accessKey} {secretKey}";
                }

                var result = await ExecuteMinioCommandAsync(command, cancellationToken);

                if (result.Success)
                {
                    Log.Information("MinIO secret created successfully for access key: {AccessKey}", accessKey);
                }
                else
                {
                    Log.Warning("Failed to create MinIO secret for access key: {AccessKey}. Error: {Error}", accessKey, result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while creating MinIO secret for access key: {AccessKey}", accessKey);
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Delete a MinIO secret (access key)
        /// </summary>
        /// <param name="accessKey">The access key to delete</param>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult indicating success or failure</returns>
        public async Task<MinioCommandResult> DeleteMinioSecretAsync(string accessKey, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureMinioClientInitializedAsync(cancellationToken);

                var command = $"mc admin user remove {_minioSettings.Alias} {accessKey}";
                var result = await ExecuteMinioCommandAsync(command, cancellationToken);

                if (result.Success)
                {
                    Log.Information("MinIO secret deleted successfully for access key: {AccessKey}", accessKey);
                }
                else
                {
                    Log.Warning("Failed to delete MinIO secret for access key: {AccessKey}. Error: {Error}", accessKey, result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while deleting MinIO secret for access key: {AccessKey}", accessKey);
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// List all MinIO secrets (access keys)
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult with the list of secrets</returns>
        public async Task<MinioCommandResult> ListMinioSecretsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureMinioClientInitializedAsync(cancellationToken);

                var command = $"mc admin user list {_minioSettings.Alias}";
                var result = await ExecuteMinioCommandAsync(command, cancellationToken);

                if (result.Success)
                {
                    Log.Information("MinIO secrets listed successfully");
                }
                else
                {
                    Log.Warning("Failed to list MinIO secrets. Error: {Error}", result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while listing MinIO secrets");
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Get information about a specific MinIO secret
        /// </summary>
        /// <param name="accessKey">The access key to get information about</param>
        /// <param name="cancellationToken"></param>
        /// <returns>MinioCommandResult with the secret information</returns>
        public async Task<MinioCommandResult> GetMinioSecretAsync(string accessKey, CancellationToken cancellationToken = default)
        {
            try
            {
                await EnsureMinioClientInitializedAsync(cancellationToken);

                var command = $"mc admin user info {_minioSettings.Alias} {accessKey}";
                var result = await ExecuteMinioCommandAsync(command, cancellationToken);

                if (result.Success)
                {
                    Log.Information("MinIO secret information retrieved successfully for access key: {AccessKey}", accessKey);
                }
                else
                {
                    Log.Warning("Failed to get MinIO secret information for access key: {AccessKey}. Error: {Error}", accessKey, result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while getting MinIO secret information for access key: {AccessKey}", accessKey);
                return new MinioCommandResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }

        #endregion

        #region MinIO Client Helper Methods

        private async Task<bool> EnsureMinioClientInitializedAsync(CancellationToken cancellationToken = default)
        {
            if (!_isMinioClientInitialized)
            {
                var command = $"mc alias set {_minioSettings.Alias ?? "myminio"} {_minioSettings.Url} {_minioSettings.AccessKey} {_minioSettings.SecretKey}";
                var result = await ExecuteMinioCommandAsync(command, cancellationToken);

                if (result.Success)
                {
                    _isMinioClientInitialized = true;
                    Log.Information("MinIO MC client initialized successfully");
                    return true;
                }
                else
                {
                    Log.Error("Failed to initialize MinIO MC client: {Error}", result.Error);
                    return false;
                }
            }
            return true;
        }

        private async Task<MinioCommandResult> ExecuteMinioCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            var result = new MinioCommandResult();

            try
            {
                Log.Debug("Executing MinIO command: {Command}", command);

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "/bin/sh",
                    Arguments = $"-c \"{command}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process();
                process.StartInfo = processStartInfo;

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        outputBuilder.AppendLine(e.Data);
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync(cancellationToken);

                result.ExitCode = process.ExitCode;
                result.Output = outputBuilder.ToString().Trim();
                result.Error = errorBuilder.ToString().Trim();
                result.Success = process.ExitCode == 0;

                if (result.Success)
                {
                    Log.Debug("MinIO command executed successfully: {Output}", result.Output);
                }
                else
                {
                    Log.Warning("MinIO command failed with exit code {ExitCode}: {Error}", result.ExitCode, result.Error);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Exception occurred while executing MinIO command: {Command}", command);
                result.Success = false;
                result.Error = ex.Message;
                return result;
            }
        }

        #endregion
    }
}
