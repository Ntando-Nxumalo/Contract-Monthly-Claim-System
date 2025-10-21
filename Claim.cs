using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract_Monthly_Claim_System.Models
{
    public class Claim
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(450)]
        public string LecturerUserId { get; set; } = null!;

        [ForeignKey(nameof(LecturerUserId))]
        public ApplicationUser? LecturerUser { get; set; }

        [Required, MaxLength(200)]
        public string LecturerName { get; set; } = null!;

        [Required]
        public double HoursWorked { get; set; }

        [Required]
        public double HourlyRate { get; set; }

        [Required]
        public double Total { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // kept for backward-compatibility (first/single document)
        [MaxLength(500)]
        public string? DocumentPath { get; set; }

        // Navigation for multiple documents
        public ICollection<ClaimDocument> Documents { get; set; } = new List<ClaimDocument>();

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}