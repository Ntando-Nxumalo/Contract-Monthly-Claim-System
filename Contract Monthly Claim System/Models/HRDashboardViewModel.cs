using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Contract_Monthly_Claim_System.Models
{
    public class HRDashboardViewModel : Controller
    {
         
        public int TotalLecturers { get; set; }
        public int PendingClaims { get; set; }
        public int ApprovedClaimsThisMonth { get; set; }
        public decimal TotalPaymentsThisMonth { get; set; }
        public List<ClaimReport> RecentClaims { get; set; }
        public List<Lecturer> Lecturers { get; set; }
    }

    public class ClaimReport
    {
        public int ClaimId { get; set; }
        public string LecturerName { get; set; }
        public string Module { get; set; }
        public DateTime ClaimDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string ApprovedBy { get; set; }
        public DateTime? ApprovalDate { get; set; }
    }

    public class Lecturer
    {
        public int LecturerId { get; set; }

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        public string LastName { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        [Phone]
        public string PhoneNumber { get; set; }

        [StringLength(50)]
        public string EmployeeNumber { get; set; }

        public DateTime DateJoined { get; set; }
        public bool IsActive { get; set; }
        public decimal HourlyRate { get; set; }

        [StringLength(20)]
        public string ContractType { get; set; }
    }

    public class PaymentReport
    {
        public int ReportId { get; set; }
        public string ReportName { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalClaims { get; set; }
        public decimal TotalAmount { get; set; }
        public DateTime GeneratedDate { get; set; }
        public string GeneratedBy { get; set; }
    }
}
