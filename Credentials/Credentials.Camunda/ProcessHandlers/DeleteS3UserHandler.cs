using Credentials.Camunda.Services;
using IMinioHelper = FiveSafesTes.Core.Services.IMinioHelper;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Deletes the ephemeral per-submission S3 (RustFS/MinIO) access key on credential expiry.
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

        protected override async Task<bool> DeleteUserAsync(string? accessKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(accessKey))
                return false;

            var result = await _minioHelper.DeleteMinioSecretAsync(accessKey, cancellationToken);
            if (!result.Success)
                return false;

            // Removing the access key detaches the policy but leaves the canned policy behind, so
            // remove the per-key policy created at provisioning ({accessKey}-policy). Best-effort:
            // the access key (the thing that grants access) is already gone, so a leftover policy
            // must not fail expiry — just warn.
            var policyName = $"{accessKey}-policy";
            var policyRemoved = await _minioHelper.RemovePolicyAsync(policyName, cancellationToken);
            if (!policyRemoved)
                _logger.LogWarning("S3 access key {AccessKey} deleted but policy {PolicyName} could not be removed",
                    accessKey, policyName);

            return true;
        }

        protected override async Task<bool> CheckUserExistAsync(string accessKey)
        {
            return await _minioHelper.UserExistsAsync(accessKey);
        }
    }
}
