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

        [MaxLength(100)]
        public string? Role { get; set; }
    }
}
