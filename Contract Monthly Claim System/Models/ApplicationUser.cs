#nullable enable
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Contract_Monthly_Claim_System.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = default!;

        // Optional duplicate of Identity Roles; kept as a property for display & convenience.
        [MaxLength(100)]
        public string? Role { get; set; }
    }
}
