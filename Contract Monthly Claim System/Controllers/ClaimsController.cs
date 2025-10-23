using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Hubs;
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Contract_Monthly_Claim_System.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<ClaimHub> _hub;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClaimsController(ApplicationDbContext db, IWebHostEnvironment env, IHubContext<ClaimHub> hub, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _env = env;
            _hub = hub;
            _userManager = userManager;
        }

        // GET: /Claims/Create
        [Authorize(Roles = "Lecturer")]
        public IActionResult Create()
        {
            return View();
        }

        public class ClaimSubmitModel
        {
            public double HoursWorked { get; set; }
            public double HourlyRate { get; set; }
            public string? Notes { get; set; }

            // Backwards-compatible single file (used by existing tests)
            public IFormFile? Document { get; set; }

            // New: accept multiple files
            public List<IFormFile>? Documents { get; set; }
        }

        // POST: /Claims/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Submit([FromForm] ClaimSubmitModel model)
        {
            if (model.HoursWorked <= 0 || model.HourlyRate < 0)
            {
                ModelState.AddModelError("", "Invalid hours or rate.");
                return RedirectToAction("LectureDashboard", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var claim = new Claim
            {
                LecturerUserId = user.Id,
                LecturerName = user.FullName ?? user.Email,
                HoursWorked = model.HoursWorked,
                HourlyRate = model.HourlyRate,
                Total = Math.Round(model.HoursWorked * model.HourlyRate, 2),
                Notes = model.Notes,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            // Persist claim first to get Id
            _db.Claims.Add(claim);
            await _db.SaveChangesAsync();

            // Work with either Documents list or single Document for compatibility
            var providedFiles = model.Documents != null && model.Documents.Count > 0
                ? model.Documents
                : (model.Document != null ? new List<IFormFile> { model.Document } : null);

            if (providedFiles != null && providedFiles.Count > 0)
            {
                var allowed = new[] { ".pdf", ".docx", ".xlsx" };
                var documentsFolder = Path.Combine(_env.WebRootPath ?? "wwwroot", "Documents");
                if (!Directory.Exists(documentsFolder)) Directory.CreateDirectory(documentsFolder);

                foreach (var doc in providedFiles)
                {
                    if (doc == null || doc.Length == 0) continue;

                    var ext = Path.GetExtension(doc.FileName).ToLowerInvariant();
                    if (!allowed.Contains(ext))
                    {
                        // skip unsupported file types
                        continue;
                    }

                    if (doc.Length > 10 * 1024 * 1024) // 10MB
                    {
                        // skip too large files
                        continue;
                    }

                    var fileName = $"{Guid.NewGuid()}{ext}";
                    var filePath = Path.Combine(documentsFolder, fileName);
                    await using (var stream = System.IO.File.Create(filePath))
                    {
                        await doc.CopyToAsync(stream);
                    }

                    var savedPath = $"/Documents/{fileName}";

                    // create ClaimDocument record
                    var docEntity = new ClaimDocument
                    {
                        ClaimId = claim.Id,
                        FileName = Path.GetFileName(doc.FileName),
                        FilePath = savedPath,
                        UploadedAt = DateTime.UtcNow
                    };
                    _db.ClaimDocuments.Add(docEntity);

                    // keep DocumentPath for legacy/preview (first one)
                    if (string.IsNullOrEmpty(claim.DocumentPath))
                    {
                        claim.DocumentPath = savedPath;
                    }
                }

                await _db.SaveChangesAsync();
            }

            // Notify coordinators in real time and the claimant
            await _hub.Clients.Group("coordinators").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);
            await _hub.Clients.Group($"user-{user.Id}").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);

            TempData["Success"] = "Claim submitted successfully.";
            return RedirectToAction("LectureDashboard", "Home");
        }

        // GET: /Claims/MyClaims
        [Authorize(Roles = "Lecturer")]
        public async Task<IActionResult> MyClaims()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var claims = await _db.Claims
                .Where(c => c.LecturerUserId == user.Id)
                .Include(c => c.Documents)
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(claims);
        }

        // GET: /Claims/ViewClaims (coordinators/managers)
        [Authorize(Roles = "Program Coordinator,Academic Manager")]
        public async Task<IActionResult> ViewClaims()
        {
            var claims = await _db.Claims
                .Include(c => c.Documents)
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(claims);
        }

        // Return single claim row partial for AJAX refresh
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Row(int id)
        {
            var claim = await _db.Claims
                .Include(c => c.Documents)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();

            return PartialView("~/Views/Claims/_ClaimRow.cshtml", claim);
        }

        // POST: /Claims/Approve
        [HttpPost]
        [Authorize(Roles = "Program Coordinator,Academic Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = "Approved";
            await _db.SaveChangesAsync();

            await _hub.Clients.Group("coordinators").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);
            await _hub.Clients.Group($"user-{claim.LecturerUserId}").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);

            if (Request.Headers.ContainsKey("X-Requested-With") && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { id, status = claim.Status });
            }
            return RedirectToAction("ViewClaims");
        }

        // POST: /Claims/Reject
        [HttpPost]
        [Authorize(Roles = "Program Coordinator,Academic Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = "Rejected";
            await _db.SaveChangesAsync();

            await _hub.Clients.Group("coordinators").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);
            await _hub.Clients.Group($"user-{claim.LecturerUserId}").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);

            if (Request.Headers.ContainsKey("X-Requested-With") && Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Ok(new { id, status = claim.Status });
            }
            return RedirectToAction("ViewClaims");
        }

        // GET: /Claims/Details/{id}
        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            var claim = await _db.Claims
                .Include(c => c.LecturerUser)
                .Include(c => c.Documents)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();

            // Simple authorization: lecturers can view only their claims; coordinators can view any.
            if (User.IsInRole("Lecturer") && claim.LecturerUserId != _userManager.GetUserId(User))
                return Forbid();

            return View(claim);
        }

        // GET: /Claims/DownloadDocument/{id}
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> DownloadDocument(int id)
        {
            var doc = await _db.ClaimDocuments
                .Include(d => d.Claim)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null || doc.Claim == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var canAccess = (User.IsInRole("Program Coordinator") || User.IsInRole("Academic Manager") ||
                             (User.IsInRole("Lecturer") && doc.Claim.LecturerUserId == currentUserId));
            if (!canAccess) return Forbid();

            // Build physical path (stored paths are like "/Documents/{guid}.ext")
            var relativePath = doc.FilePath?.TrimStart('/') ?? string.Empty;
            var root = _env.WebRootPath ?? "wwwroot";
            var physicalPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(physicalPath)) return NotFound();

            var contentType = extToContentType(Path.GetExtension(physicalPath));
            var fileBytes = await System.IO.File.ReadAllBytesAsync(physicalPath);
            return File(fileBytes, contentType, doc.FileName);
        }

        private static string extToContentType(string ext)
        {
            ext = ext.ToLowerInvariant();
            return ext switch
            {
                ".pdf" => "application/pdf",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
        }
    }
}
