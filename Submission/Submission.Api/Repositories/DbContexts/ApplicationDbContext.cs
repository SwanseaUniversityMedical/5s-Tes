using FiveSafesTes.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Submission.Api.Repositories.DbContexts
{
    public class ApplicationDbContext : DbContext
    {
        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    optionsBuilder.UseNpgsql("DefaultConnection")
        //    .UseUtcDateTime();
        //}
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
          : base(options)
        {
           
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Project> Projects { get; set; }
        
        
        public DbSet<Tre> Tres { get; set; }

        public DbSet<FiveSafesTes.Core.Models.Submission> Submissions { get; set; }
        public DbSet<HistoricStatus> HistoricStatuses { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<SubmissionFile> SubmissionFiles { get; set; }

        public DbSet<ProjectTreDecision> ProjectTreDecisions { get; set; }

        public DbSet<MembershipTreDecision> MembershipTreDecisions { get; set; }

        public DbSet<UsedOnboardingJti> UsedOnboardingJtis { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<UsedOnboardingJti>(b =>
            {
                b.HasIndex(x => x.Jti).IsUnique();
            });
        }
    }
}
