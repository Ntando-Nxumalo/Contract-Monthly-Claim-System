#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Contract_Monthly_Claim_System.Models
{
    public class HRDashboardVM
    {
        public int TotalApprovedClaims { get; set; }
        public int PendingPayments { get; set; }
        public decimal TotalPayableAmount { get; set; }
        public List<Claim> ApprovedClaims { get; set; } = new();
        public List<ApplicationUser> Lecturers { get; set; } = new();
    }

    public class LecturerProfileVM
    {
        [Required]
        public ApplicationUser Lecturer { get; set; } = default!;
        public List<Claim> Claims { get; set; } = new();

        [EmailAddress]
        public string? NewEmail { get; set; }

        [Phone]
        public string? NewPhoneNumber { get; set; }
    }

    public class ApprovedClaimSummaryDTO
    {
        public int ClaimId { get; set; }
        public string LecturerName { get; set; } = string.Empty;
        public DateTime ClaimDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
