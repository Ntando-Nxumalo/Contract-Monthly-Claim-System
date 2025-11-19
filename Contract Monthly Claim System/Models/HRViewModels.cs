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

    public class ClaimInvoiceVM
    {
        public Claim Claim { get; set; } = default!;
        public string InvoiceNumber => Claim != null ? $"INV-{Claim.Id:000}" : "INV-000";
        public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;
        public string CompanyName { get; set; } = "Contract Monthly Claim System";
        public string CompanyAddress { get; set; } = "123 Campus Drive, Johannesburg";
        public string CompanyEmail { get; set; } = "hr@contractclaims.local";
        public string CompanyPhone { get; set; } = "+27 11 555 0100";
    }

    public class PayslipPreviewVM
    {
        public Claim Claim { get; set; } = default!;
        public DateTime GeneratedOn { get; set; } = DateTime.UtcNow;
        public string CompanyName { get; set; } = "Contract Monthly Claim System";
        public string CompanyAddress { get; set; } = "123 Campus Drive, Johannesburg";
    }
}
