#nullable enable
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Contract_Monthly_Claim_System.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Domain sets
        public DbSet<Claim> Claims { get; set; } = null!;
        public DbSet<Lecturer> Lecturers { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Claim entity defaults
            builder.Entity<Claim>(b =>
            {
                b.Property(p => p.Status).HasMaxLength(50).HasDefaultValue("Pending");
                b.Property(p => p.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            // Minimal seed (optional) - update as needed
            builder.Entity<Lecturer>().HasData(
                new Lecturer { Id = 1, Name = "Dr. Sarah Johnson", Email = "sarah@uni.edu", Role = "Lecturer" }
            );
        }
    }
}
