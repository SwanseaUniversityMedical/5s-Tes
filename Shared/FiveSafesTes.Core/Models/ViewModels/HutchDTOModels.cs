using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class SubmitJobModel
    {
        /// <summary>
        /// This is a Job ID as provided by the TRE Agent.
        /// Hutch continues to use it to identify the job internally,
        /// but also to interact with the TRE Agent in future.
        /// </summary>
        [Required]
        public string SubId { get; set; } = string.Empty;

        /// <summary>
        /// Optional Project Database Connection details.
        /// This allows the requested workflow to be granted access to the
        /// data source it should run against, if any.
        /// </summary>
        public DatabaseConnectionDetails? DataAccess { get; set; }

        /// <summary>
        /// <para>
        /// Optional absolute URL where the Job's RO-Crate can be fetched from with a GET request.
        /// </para>
        ///
        /// <para>Mutually exclusive with <see cref="CrateUrl"/></para>
        ///
        /// <para>
        /// If all crate values are omitted at the time of Submission,
        /// a later submission can be made to provide a Remote URL, Cloud Storage details, or a binary crate payload.
        /// </para>
        /// </summary>
        public Uri? CrateUrl { get; set; }

        /// <summary>
        /// <para>
        /// Optional details for where the crate can be found in a Cloud Storage Provider.
        /// </para>
        ///
        /// <para>Mutually exclusive with <see cref="CrateUrl"/></para>
        ///
        /// <para>
        /// If all crate values are omitted at the time of Submission,
        /// a later submission can be made to provide a Remote URL, Cloud Storage details, or a binary crate payload.
        /// </para>
        /// </summary>
        public FileStorageDetails? CrateSource { get; set; }
    }

    public class JobStatusModel
    {
        /// <summary>
        /// The Id of the Job
        /// </summary>
        public  string Id { get; set; }

        /// <summary>
        /// The current status of the Job
        /// </summary>
        public  string Status { get; set; }
    }

    public class FileStorageDetails
    {
        /// <summary>
        /// Cloud Storage Host e.g. a Min.io Server. Defaults to preconfigured value if omitted.
        /// </summary>
        public string Host { get; set; } = string.Empty;

        /// <summary>
        /// Connection to <see cref="Host"/> should use SSL.
        /// Defaults to true; should only really be false for development/testing.
        /// </summary>
        public bool Secure { get; set; } = true;

        /// <summary>
        /// Cloud Storage Container name e.g. a Min.io Bucket. Defaults to preconfigured value if omitted.
        /// </summary>
        public string Bucket { get; set; } = string.Empty;

        /// <summary>
        /// Object ID for a single Cloud Storage item in the named <see cref="Bucket"/>,
        /// functionally the "Path" to the stored file.
        /// </summary>
        [Required]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Access Key for Cloud Storage. If omitted, will use OIDC if configured, or default to preconfigured value if not.
        /// </summary>
        public string AccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Secret Key for Cloud Storage. If omitted, will use OIDC if configured, or default to preconfigured value if not.
        /// </summary>
        public string SecretKey { get; set; } = string.Empty;
    }
    public class DatabaseConnectionDetails
    {
        /// <summary>
        /// Database Server Hostname
        /// </summary>
        [Required]
        public string Hostname { get; set; } = string.Empty;

        /// <summary>
        /// Database Server Port. Defaults to PostgreSQL Default (5432)
        /// </summary>
        public int Port { get; set; } = 5432;

        /// <summary>
        /// Name of the Database to connect to
        /// </summary>
        [Required]
        public string Database { get; set; } = string.Empty;

        /// <summary>
        /// Username with access to the database
        /// </summary>
        [Required]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Password with access to the database
        /// </summary>
        [Required]
        public string Password { get; set; } = string.Empty;
    }

    public class ApprovalResult
    {

        public string Host { get; set; }
        public string Bucket  { get; set; }

        public bool Secure { get; set; }

        public string Path { get; set; }
        public ApprovalType Status { get; set; }

        public Dictionary<string, bool> FileResults { get; set; } = new();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ApprovalType
    {
        FullyApproved,
        PartiallyApproved,
        NotApproved
    }
}
