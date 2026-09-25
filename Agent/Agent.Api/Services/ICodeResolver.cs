namespace Agent.Api.Services
{
    /// <summary>
    /// Supplies the value behind each code found in a TES message.
    /// The codes DMN is evaluated by the Camunda process, which writes every resolved value into
    /// Vault under project/taskId keyed by code. The implementation of this interface reads them
    /// back.
    /// </summary>
    public interface ICodeResolver
    {
        /// <summary>
        /// Resolves the given codes for one task. Codes are already lower-cased and de-duplicated.
        /// Codes that cannot be resolved are omitted from the result rather than returned blank,
        /// so the caller can fail the job.
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> ResolveAsync(
            string project,
            string taskId,
            IReadOnlyCollection<string> codes,
            CancellationToken cancellationToken = default);
    }
}
