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

            // Optional: explicitly map to table name if needed
            builder.Entity<Claim>().ToTable("Claims");
            builder.Entity<Lecturer>().ToTable("Lecturers");
        }
    }
}