using Credentials.Models.Models;
using Microsoft.EntityFrameworkCore;

namespace Credentials.Models.DbContexts
{
    public class CredentialsDbContext : DbContext
    {
        public CredentialsDbContext(DbContextOptions<CredentialsDbContext> options) : base(options)
        {
        }

        public DbSet<EphemeralCredential> EphemeralCredentials { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<EphemeralCredential>()
                .HasIndex(m => new { m.SubmissionId, m.ProcessInstanceKey })
                .IsUnique(false);           
        }
    }
}
