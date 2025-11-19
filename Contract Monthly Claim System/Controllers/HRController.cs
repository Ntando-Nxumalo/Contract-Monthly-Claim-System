#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Models;
using Contract_Monthly_Claim_System.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Contract_Monthly_Claim_System.Controllers
{
    [Authorize(Roles = "HR")]
    public class HRController : Controller
    {
        private readonly HRService _hrService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public HRController(HRService hrService, UserManager<ApplicationUser> userManager, ApplicationDbContext db, IWebHostEnvironment env)
        {
            _hrService = hrService;
            _userManager = userManager;
            _db = db;
            _env = env;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int claimId)
        {
            var claim = await _db.Claims.FindAsync(claimId);
            if (claim == null) return NotFound();

            if (!string.Equals(claim.Status, "Paid", StringComparison.OrdinalIgnoreCase))
            {
                claim.Status = "Paid";
                await _db.SaveChangesAsync();
                TempData["Success"] = $"Claim CLM-{claim.Id:000} marked as paid.";
            }

            return RedirectToAction(nameof(Dashboard));
        }

        [HttpGet]
        public async Task<IActionResult> ExportClaimPdf(int claimId)
        {
            var claim = await _db.Claims
                .Include(c => c.LecturerUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return NotFound();

            var pdfBytes = BuildSimplePdf(claim);
            var fileName = $"Claim_{claim.Id:000}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportClaimExcel(int claimId)
        {
            var claim = await _db.Claims
                .Include(c => c.LecturerUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return NotFound();

            var sb = new StringBuilder();
            sb.AppendLine("ClaimId,Lecturer,Hours,HourlyRate,Total,Status,Date");
            sb.AppendLine($"{claim.Id},\"{claim.LecturerName.Replace("\"", "\"\"")}\",{claim.HoursWorked},{claim.HourlyRate},{claim.Total},{claim.Status},{(claim.DateOfExpense?.ToString("yyyy-MM-dd") ?? "-")}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var fileName = $"Claim_{claim.Id:000}.csv";
            return File(bytes, "text/csv", fileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendPayslip(int claimId)
        {
            var claim = await _db.Claims
                .Include(c => c.LecturerUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == claimId);

            if (claim == null) return NotFound();

            var directory = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "Payslips");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var payload = BuildPayslipPayload(claim);
            var filePath = Path.Combine(directory, $"Payslip_{claim.Id:000}_{DateTime.UtcNow:yyyyMMddHHmmss}.txt");
            await System.IO.File.WriteAllTextAsync(filePath, payload);

            TempData["Success"] = $"Payslip saved and ready to email for {claim.LecturerName}.";
            return RedirectToAction(nameof(Dashboard));
        }

        private static string BuildPayslipPayload(Claim claim)
        {
            var culture = new CultureInfo("en-ZA");
            var sb = new StringBuilder();
            sb.AppendLine($"To: {claim.LecturerUser?.Email ?? "unknown"}");
            sb.AppendLine($"Subject: Payslip for claim CLM-{claim.Id:000}");
            sb.AppendLine();
            sb.AppendLine($"Dear {claim.LecturerName},");
            sb.AppendLine($"Amount due: {claim.Total.ToString("C2", culture)}");
            sb.AppendLine($"Hours: {claim.HoursWorked}");
            sb.AppendLine($"Hourly Rate: {claim.HourlyRate.ToString("C2", culture)}");
            sb.AppendLine($"Status: {claim.Status}");
            sb.AppendLine();
            sb.AppendLine("Regards,");
            sb.AppendLine("HR Department");
            return sb.ToString();
        }

        private static byte[] BuildSimplePdf(Claim claim)
        {
            var culture = new CultureInfo("en-ZA");
            var lines = new[]
            {
                $"Claim ID: CLM-{claim.Id:000}",
                $"Lecturer : {claim.LecturerName}",
                $"Hours    : {claim.HoursWorked}",
                $"Rate     : {claim.HourlyRate.ToString("C2", culture)}",
                $"Total    : {claim.Total.ToString("C2", culture)}",
                $"Status   : {claim.Status}",
                $"Date     : {(claim.DateOfExpense?.ToString("yyyy-MM-dd") ?? "-")}"
            };
            var contentString = string.Join(" | ", lines).Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.ASCII, 1024, leaveOpen: true);
            writer.NewLine = "\n";
            writer.WriteLine("%PDF-1.4");
            writer.Flush();

            var offsets = new List<long>();
            void WriteObject(string obj)
            {
                offsets.Add(ms.Position);
                writer.WriteLine(obj);
                writer.Flush();
            }

            WriteObject("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj");
            WriteObject("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj");
            WriteObject("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj");

            var streamContent = $"BT /F1 12 Tf 72 720 Td ({contentString}) Tj ET";
            WriteObject($"4 0 obj\n<< /Length {streamContent.Length} >>\nstream\n{streamContent}\nendstream\nendobj");

            WriteObject("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj");

            var startXref = ms.Position;
            writer.WriteLine("xref");
            writer.WriteLine($"0 {offsets.Count + 1}");
            writer.WriteLine("0000000000 65535 f ");
            foreach (var offset in offsets)
            {
                writer.WriteLine($"{offset:D10} 00000 n ");
            }
            writer.WriteLine("trailer");
            writer.WriteLine($"<< /Size {offsets.Count + 1} /Root 1 0 R >>");
            writer.WriteLine("startxref");
            writer.WriteLine(startXref);
            writer.WriteLine("%%EOF");
            writer.Flush();

            return ms.ToArray();
        }
    }
}
