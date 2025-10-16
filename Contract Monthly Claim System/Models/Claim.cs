// Claim model representing a lecturer's claim submission
#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract_Monthly_Claim_System.Models
{
    public class Claim
    {
        public int Id { get; set; }

        // FK to ApplicationUser (student)
        [Required]
        public string StudentId { get; set; } = default!;

        [Required, MaxLength(200)]
        public string LecturerName { get; set; } = default!;

        [Required]
        public double HoursWorked { get; set; }

        [Required]
        public double HourlyRate { get; set; }

        [Required]
        public double Total { get; set; }

        [MaxLength(500)]
        public string? Notes { get; set; }

        // stored relative path e.g. /Documents/{guid}.pdf
        [MaxLength(500)]
        public string? DocumentPath { get; set; }

        [Required, MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey(nameof(StudentId))]
        public ApplicationUser? Student { get; set; }
    }
}