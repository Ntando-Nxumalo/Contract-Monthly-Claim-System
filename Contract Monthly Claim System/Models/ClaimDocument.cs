using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Contract_Monthly_Claim_System.Models
{
    public class ClaimDocument
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ClaimId { get; set; }

        [ForeignKey(nameof(ClaimId))]
        public Claim? Claim { get; set; }

        [Required, MaxLength(500)]
        public string FilePath { get; set; } = null!;

        [Required, MaxLength(200)]
        public string FileName { get; set; } = null!;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}