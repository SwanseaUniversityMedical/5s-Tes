using Amazon.S3.Model;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using Microsoft.AspNetCore.Http;

namespace FiveSafesTes.Core.Services
{
    public interface IMinioHelper
    {
        Task<bool> CheckBucketExists(string bucketName = "");
        Task<bool> CreateBucket(string bucketName = "");
        Task<bool> UploadFileAsync(IFormFile? filePath, string bucketName = "", string objectName = "");
        Task<bool> DownloadFileAsync(string bucketName = "", string objectName = "");
        Task<bool> CheckObjectExists(string bucketName, string objectKey);
        Task<bool> FetchAndStoreObject(string url, string bucketName, string key);
        Task<bool> RabbitExternalObject(MQFetchFile msgBytes);
        /// <summary>
        /// No-op on Ceph RGW (no detached canned policies); per-user grants are applied by
        /// <see cref="CreateProjectS3AccessPolicyAsync"/>.
        /// </summary>
        Task<bool> CreateBucketPolicy(string bucketName);

        Task<bool> CopyObjectToDestination(string destinationBucketName, string destinationObjectKey,
            GetObjectResponse response);

        Task<GetObjectResponse> GetCopyObject(string sourceBucketName, string sourceObjectKey);
        Task<string> ShareMinioObject(string bucketName, string objectKey);
        Task<bool> FolderExists(string bucketName, string folderName);
        Task<bool> CreateFolder(string bucketName, string folderName);

        Task<ListObjectsV2Response> GetFilesInBucket(string bucketName, string prefix = "");

        Task<bool> SetPublicPolicy(string bucketName);

        Task<bool> BucketPolicySetPublic(string bucketName);

        Task DeleteObject(string bucketName, string objectKey);

        /// <summary>
        /// Removes every object from a bucket without removing the bucket itself.
        /// </summary>
        Task<bool> EmptyBucketAsync(string bucketName);

        /// <summary>
        /// Deletes a bucket entirely (empties it first, since S3 requires an empty bucket).
        /// Idempotent: returns true when the bucket does not exist.
        /// </summary>
        Task<bool> DeleteBucketAsync(string bucketName);

        Task WriteToStore(string bucketName, string objectKey, MemoryStream file);

        Task<MinioCommandResult> CreateMinioSecretAsync(string accessKey, string secretKey = "", CancellationToken cancellationToken = default);
        Task<MinioCommandResult> DeleteMinioSecretAsync(string accessKey, CancellationToken cancellationToken = default);
        Task<MinioCommandResult> ListMinioSecretsAsync(CancellationToken cancellationToken = default);
        Task<MinioCommandResult> GetMinioSecretAsync(string accessKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// Grants <paramref name="accessKey"/> read on the submission bucket and read/write/delete on
        /// the output bucket by merging Principal-scoped statements (Sids tagged with
        /// <paramref name="policyName"/>) into each bucket's policy. The user must already exist.
        /// </summary>
        Task<bool> CreateProjectS3AccessPolicyAsync(string policyName, string accessKey, string submissionBucket, string outputBucket);

        /// <summary>
        /// Strips the statements tagged with <paramref name="policyName"/> from the submission and
        /// output bucket policies (cleanup for an access key's grants when it is revoked).
        /// </summary>
        Task<bool> RemovePolicyAsync(string policyName, string submissionBucket, string outputBucket, CancellationToken cancellationToken = default);

        /// <summary>
        /// Returns true when an S3 access key (user) already exists on the object store.
        /// </summary>
        Task<bool> UserExistsAsync(string accessKey, CancellationToken cancellationToken = default);
    }
}
