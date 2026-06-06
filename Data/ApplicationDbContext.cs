using Microsoft.EntityFrameworkCore;
using App_thi_tin_hoc.Models;

namespace App_thi_tin_hoc.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Contest> Contests { get; set; }
        public DbSet<Problem> Problems { get; set; }
        public DbSet<Testcase> Testcases { get; set; }
        public DbSet<Submission> Submissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cascade delete configurations
            modelBuilder.Entity<Problem>()
                .HasOne(p => p.Contest)
                .WithMany(c => c.Problems)
                .HasForeignKey(p => p.ContestId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Testcase>()
                .HasOne(t => t.Problem)
                .WithMany(p => p.Testcases)
                .HasForeignKey(t => t.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Account)
                .WithMany(a => a.Submissions)
                .HasForeignKey(s => s.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Problem)
                .WithMany(p => p.Submissions)
                .HasForeignKey(s => s.ProblemId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Submission>()
                .HasOne(s => s.Contest)
                .WithMany(c => c.Submissions)
                .HasForeignKey(s => s.ContestId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure all DateTime properties to have Utc kind when loaded
            var dateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
                v => v,
                v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTime))
                    {
                        property.SetValueConverter(dateTimeConverter);
                    }
                    else if (property.ClrType == typeof(DateTime?))
                    {
                        var nullableDateTimeConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime?, DateTime?>(
                            v => v,
                            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null);
                        property.SetValueConverter(nullableDateTimeConverter);
                    }
                }
            }
        }
    }
}
