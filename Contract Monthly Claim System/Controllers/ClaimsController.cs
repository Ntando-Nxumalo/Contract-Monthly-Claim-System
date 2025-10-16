using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Hubs;
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
        [Authorize(Roles = "Student")]
        public IActionResult Create()
        {
            return View();
        }

        public class ClaimSubmitModel
        {
            public double HoursWorked { get; set; }
            public double HourlyRate { get; set; }
            public string? Notes { get; set; }
            public IFormFile? Document { get; set; }
        }

        // POST: /Claims/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Submit([FromForm] ClaimSubmitModel model)
        {
            if (model.HoursWorked <= 0 || model.HourlyRate < 0)
            {
                ModelState.AddModelError("", "Invalid hours or rate.");
                return RedirectToAction("LectureDashboard", "Home");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            // Save file if provided
            string? savedPath = null;
            if (model.Document != null && model.Document.Length > 0)
            {
                var allowed = new[] { ".pdf", ".docx", ".xlsx" };
                var ext = Path.GetExtension(model.Document.FileName).ToLowerInvariant();
                if (!allowed.Contains(ext))
                {
                    ModelState.AddModelError("", "File type not allowed.");
                    return RedirectToAction("LectureDashboard", "Home");
                }

                var documentsFolder = Path.Combine(_env.WebRootPath, "Documents");
                if (!Directory.Exists(documentsFolder)) Directory.CreateDirectory(documentsFolder);

                var fileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(documentsFolder, fileName);
                await using (var stream = System.IO.File.Create(filePath))
                {
                    await model.Document.CopyToAsync(stream);
                }
                savedPath = $"/Documents/{fileName}";
            }

            var claim = new Claim
            {
                StudentId = user.Id,
                LecturerName = user.FullName ?? user.Email,
                HoursWorked = model.HoursWorked,
                HourlyRate = model.HourlyRate,
                Total = Math.Round(model.HoursWorked * model.HourlyRate, 2),
                Notes = model.Notes,
                DocumentPath = savedPath,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _db.Claims.Add(claim);
            await _db.SaveChangesAsync();

            // Notify coordinators in real time
            await _hub.Clients.Group("coordinators").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);

            TempData["Success"] = "Claim submitted successfully.";
            return RedirectToAction("LectureDashboard", "Home");
        }

        // GET: /Claims/MyClaims
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyClaims()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Forbid();

            var claims = await _db.Claims
                .Where(c => c.StudentId == user.Id)
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(claims);
        }

        // GET: /Claims/ViewClaims (coordinators)
        [Authorize(Roles = "Academic Coordinator,Program Coordinator")]
        public async Task<IActionResult> ViewClaims()
        {
            var claims = await _db.Claims
                .OrderByDescending(c => c.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            return View(claims);
        }

        // POST: /Claims/Approve
        [HttpPost]
        [Authorize(Roles = "Academic Coordinator,Program Coordinator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = "Approved";
            await _db.SaveChangesAsync();

            await _hub.Clients.Group("coordinators").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);
            await _hub.Clients.Group($"user-{claim.StudentId}").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);

            return RedirectToAction("ViewClaims");
        }

        // POST: /Claims/Reject
        [HttpPost]
        [Authorize(Roles = "Academic Coordinator,Program Coordinator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var claim = await _db.Claims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status = "Rejected";
            await _db.SaveChangesAsync();

            await _hub.Clients.Group("coordinators").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);
            await _hub.Clients.Group($"user-{claim.StudentId}").SendAsync("ReceiveClaimStatusUpdate", claim.Id, claim.Status);

            return RedirectToAction("ViewClaims");
        }

        // GET: /Claims/Details/{id}
        [Authorize]
        public async Task<IActionResult> Details(int id)
        {
            var claim = await _db.Claims
                .Include(c => c.Student)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (claim == null) return NotFound();

            // Simple authorization: students can view only their claims; coordinators can view any.
            if (User.IsInRole("Student") && claim.StudentId != _userManager.GetUserId(User))
                return Forbid();

            return View(claim);
        }
    }
}
