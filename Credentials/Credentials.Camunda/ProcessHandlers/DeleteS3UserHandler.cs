using Credentials.Camunda.Services;
using IMinioHelper = FiveSafesTes.Core.Services.IMinioHelper;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Deletes the ephemeral per-submission S3 (Ceph RGW) access key on credential expiry.
    /// The base class handles the existence check and Vault cleanup.
    /// </summary>
    [JobType("delete-s3-user")]
    public class DeleteS3UserHandler : DeleteCredentialHandlerBase
    {
        private readonly IMinioHelper _minioHelper;
        protected override string CredentialType => "s3";

        public DeleteS3UserHandler(
            ILogger<DeleteS3UserHandler> logger,
            IMinioHelper minioHelper,
            IVaultCredentialsService vaultCredentialsService,
            IEphemeralCredentialsService ephemeralCredentialsService)
            : base(logger, vaultCredentialsService, ephemeralCredentialsService)
        {
            _minioHelper = minioHelper;
        }

        protected override async Task<bool> DeleteUserAsync(string? accessKey, string vaultPath, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(accessKey))
                return false;

            // Read the buckets back before the Vault entry is removed: the grants live as
            // statements on these buckets' policies, not on a named policy object.
            var credential = await _vaultCredentialsService.GetCredentialAsync(vaultPath);
            var submissionBucket = credential.GetValueOrDefault("submissionBucket")?.ToString() ?? string.Empty;
            var outputBucket = credential.GetValueOrDefault("outputBucket")?.ToString() ?? string.Empty;

            var result = await _minioHelper.DeleteMinioSecretAsync(accessKey, cancellationToken);
            if (!result.Success)
                return false;

            // Deleting the user leaves its Principal-scoped statements on the bucket policies, so
            // strip the ones tagged at provisioning ({accessKey}-policy). Best-effort: the access
            // key (the thing that grants access) is already gone, so leftover statements must not
            // fail expiry — just warn.
            var policyName = $"{accessKey}-policy";
            var policyRemoved = await _minioHelper.RemovePolicyAsync(policyName, submissionBucket, outputBucket, cancellationToken);
            if (!policyRemoved)
                _logger.LogWarning("S3 access key {AccessKey} deleted but policy {PolicyName} statements could not be removed from buckets {SubmissionBucket}/{OutputBucket}",
                    accessKey, policyName, submissionBucket, outputBucket);

            return true;
        }

        protected override async Task<bool> CheckUserExistAsync(string accessKey)
        {
            return await _minioHelper.UserExistsAsync(accessKey);
        }
    }
}
