using System.Security.Cryptography;
using FiveSafesTes.Core.Constants;
using FiveSafesTes.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog;

namespace FiveSafesTes.Core.Services
{
    /// <summary>
    /// Creates and retrieves per-project scoped S3 credentials on the Submission Layer.
    /// Runs on Submission.Api only (uses root MinIO creds to provision users/policies).
    /// </summary>
    public class ProjectS3AccessKeyService : IProjectS3AccessKeyService
    {
        private readonly IMinioHelper _minioHelper;
        private readonly IVaultCredentialsService _vaultCredentialsService;
        private readonly ILogger<ProjectS3AccessKeyService> _logger;

        public ProjectS3AccessKeyService(
            IMinioHelper minioHelper,
            IVaultCredentialsService vaultCredentialsService,
            ILogger<ProjectS3AccessKeyService> logger)
        {
            _minioHelper = minioHelper;
            _vaultCredentialsService = vaultCredentialsService;
            _logger = logger;
        }

        public async Task<ProjectS3AccessKey?> GetAccessKeyAsync(int projectId)
        {
            var path = S3AccessKeyVaultPaths.ForProject(projectId);
            var data = await _vaultCredentialsService.GetCredentialAsync(path);

            return ProjectS3AccessKey.TryFromVault(projectId, data);
        }

        /// <summary>
        /// Returns existing credentials from Submission Vault, or creates them on RustFS if missing.
        /// </summary>
        public async Task<ProjectS3AccessKey?> EnsureAccessKeyAsync(
            int projectId,
            string projectName,
            string submissionBucket,
            string outputBucket)
        {
            var existing = await GetAccessKeyAsync(projectId);
            if (existing != null &&
                !string.IsNullOrWhiteSpace(existing.AccessKey) &&
                !string.IsNullOrWhiteSpace(existing.SecretKey))
            {
                return existing;
            }

            if (string.IsNullOrWhiteSpace(submissionBucket) || string.IsNullOrWhiteSpace(outputBucket))
            {
                _logger.LogError(
                    "Cannot create S3 access key for project {ProjectId}: missing bucket names",
                    projectId);
                return null;
            }


            var accessKeyId = BuildAccessKeyName(projectId);
            var secretKey = GenerateSecretKey();
            var policyName = BuildPolicyName(projectId);

            // Step 1: Create an IAM-style policy scoped to this project's two buckets only.
            var policyCreated = await _minioHelper.CreateProjectS3AccessPolicyAsync(
                policyName, submissionBucket, outputBucket);
            if (!policyCreated)
            {
                _logger.LogError(
                    "Failed to create S3 access policy {PolicyName} for project {ProjectId}",
                    policyName, projectId);
                return null;
            }

            // Step 2: Create a RustFS/MinIO user (accessKeyId + secretKey pair) via mc admin.
            var userCreated = await _minioHelper.CreateMinioSecretAsync(accessKeyId, secretKey);
            if (!userCreated.Success)
            {
                _logger.LogError(
                    "Failed to create S3 user {AccessKeyId} for project {ProjectId}: {Error}",
                    accessKeyId, projectId, userCreated.Error);
                return null;
            }

            // Step 3: Attach the scoped policy so the user can only reach this project's buckets.
            var policyAttached = await _minioHelper.AttachPolicyToUserAsync(policyName, accessKeyId);
            if (!policyAttached)
            {
                _logger.LogError(
                    "Failed to attach policy {PolicyName} to user {AccessKeyId} for project {ProjectId}",
                    policyName, accessKeyId, projectId);
                return null;
            }

            var credentials = new ProjectS3AccessKey
            {
                ProjectId = projectId,
                ProjectName = projectName,
                AccessKey = accessKeyId,
                SecretKey = secretKey,
                SubmissionBucket = submissionBucket,
                OutputBucket = outputBucket
            };

            var stored = await _vaultCredentialsService.AddCredentialAsync(
                S3AccessKeyVaultPaths.ForProject(projectId),
                credentials.ToVaultDictionary("createdAt", DateTime.UtcNow.ToString("o")));

            if (!stored)
            {
                _logger.LogError(
                    "Created S3 credentials for project {ProjectId} but failed to store in Vault",
                    projectId);
                return null;
            }

            Log.Information(
                "Created scoped S3 credentials for project {ProjectId} at vault path {VaultPath}",
                projectId,
                S3AccessKeyVaultPaths.ForProject(projectId));

            return credentials;
        }

        public static string BuildAccessKeyName(int projectId) => $"proj-{projectId}-s3";

        public static string BuildPolicyName(int projectId) => $"project-{projectId}-s3-policy";

        private static string GenerateSecretKey()
        {
            var bytes = new byte[32];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', 'x')
                .Replace('/', 'y');
        }

    }
}
