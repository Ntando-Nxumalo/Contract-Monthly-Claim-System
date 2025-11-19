#nullable enable
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Models;
using Contract_Monthly_Claim_System.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Contract_Monthly_Claim_System.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly HRService _hrService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;

        public HRController(HRService hrService, UserManager<ApplicationUser> userManager, ApplicationDbContext db)
        {
            _hrService = hrService;
            _userManager = userManager;
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var vm = await _hrService.GetDashboardAsync();
            return View("~/Views/HR/Dashboard.cshtml", vm);
        }

        [HttpGet]
        public async Task<IActionResult> LecturerProfile(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();
            var vm = await _hrService.GetLecturerProfileAsync(id);
            if (vm == null) return NotFound();
            return View("~/Views/HR/LecturerProfile.cshtml", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLecturer(string id, string? newEmail, string? newPhoneNumber)
        {
            if (string.IsNullOrWhiteSpace(id)) return BadRequest();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(newEmail) && !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = newEmail;
                user.UserName = newEmail; // keep aligned with existing pattern
                user.NormalizedEmail = newEmail.ToUpperInvariant();
                user.NormalizedUserName = newEmail.ToUpperInvariant();
            }
            if (!string.IsNullOrWhiteSpace(newPhoneNumber))
            {
                user.PhoneNumber = newPhoneNumber;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            }
            else
            {
                TempData["Success"] = "Lecturer profile updated.";
            }

            return RedirectToAction(nameof(LecturerProfile), new { id });
        }

        [HttpGet]
        public async Task<IActionResult> ApprovedClaimsCsv()
        {
            var rows = await _hrService.GetApprovedSummariesAsync();
            var sb = new StringBuilder();
            sb.AppendLine("ClaimId,LecturerName,ClaimDate,TotalAmount,Status");
            foreach (var r in rows)
            {
                // Use ISO date and dot decimal for CSV
                var date = r.ClaimDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                var amt = r.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
                sb.AppendLine($"{r.ClaimId},\"{r.LecturerName.Replace("\"", "\"\"")}\",{date},{amt},{r.Status}");
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", $"ApprovedClaims_{DateTime.UtcNow:yyyyMMddHHmm}.csv");
        }

        [HttpGet]
        public async Task<IActionResult> ApprovedClaimsReport()
        {
            var rows = await _hrService.GetApprovedSummariesAsync();
            ViewData["GeneratedOn"] = DateTime.UtcNow;
            return View("~/Views/HR/Reports/ApprovedClaimsReport.cshtml", rows);
        }
    }
}
