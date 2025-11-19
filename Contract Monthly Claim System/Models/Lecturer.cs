using System.ComponentModel.DataAnnotations;

namespace ContractMonthlyClaimSystem.Models
{
    public class Lecturer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [Phone]
        public string? Phone { get; set; }

        // Optional: simple FK or department indicator
        [Range(0, int.MaxValue)]
        public int? DepartmentId { get; set; }
    }
}