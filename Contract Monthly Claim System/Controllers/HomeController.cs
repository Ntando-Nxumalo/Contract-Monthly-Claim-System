using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Contract_Monthly_Claim_System.Hubs;
using Microsoft.AspNetCore.Mvc;
using AuthClaim = System.Security.Claims.Claim;
using ContractClaim = Contract_Monthly_Claim_System.Models.Claim;
using Contract_Monthly_Claim_System.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Contract_Monthly_Claim_System.Models;

namespace Contract_Monthly_Claim_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public HomeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // GET: Home/Index (Login Page)
        public IActionResult Index()
        {
            return View();
        }

        // GET: Home/Register (Registration Page)
        public IActionResult Register()
        {
            return View();
        }

        // POST: Home/Login
        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            // Basic validation
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Invalid login attempt";
                return View("Index");
            }

            // Try find user by email
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                // Create a user for dev/demo flows (password required)
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = email,
                    EmailConfirmed = true,
                    Role = "Lecturer"
                };

                var createResult = await _userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    ViewBag.Error = "Unable to create user: " + string.Join("; ", createResult.Errors.Select(e => e.Description));
                    return View("Index");
                }

                // ensure role assignment (roles are seeded at startup in Program.cs)
                if (!string.IsNullOrEmpty(user.Role))
                {
                    await _userManager.AddToRoleAsync(user, user.Role);
                }
            }
            else
            {
                // Validate password and sign-in using Identity 
                var signInResult = await _signInManager.PasswordSignInAsync(user.UserName, password, isPersistent: false, lockoutOnFailure: false);
                if (!signInResult.Succeeded)
                {
                    ViewBag.Error = "Invalid login attempt";
                    return View("Index");
                }
            }

            // Sign-in (ensures NameIdentifier claim is present and SignalR Context.UserIdentifier will work)
            await _signInManager.SignInAsync(user, isPersistent: false);

            return RedirectToAction("Dashboard", "Home");
        }

        // POST: Home/Register
        [HttpPost]
        public async Task<IActionResult> Register(string name, string role, string email, string password)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Please fill all required fields";
                return View("Register");
            }

            var existing = await _userManager.FindByEmailAsync(email);
            if (existing != null)
            {
                ViewBag.Error = "A user with that email already exists";
                return View("Register");
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = name,
                EmailConfirmed = true,
                Role = string.IsNullOrEmpty(role) ? "Lecturer" : role
            };

            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                ViewBag.Error = "Registration failed: " + string.Join("; ", result.Errors.Select(e => e.Description));
                return View("Register");
            }

            if (!string.IsNullOrEmpty(user.Role))
            {
                await _userManager.AddToRoleAsync(user, user.Role);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Dashboard", "Home");
        }

        // Dashboard after successful login
        [HttpGet]
        public IActionResult Dashboard()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            // Provide coordinator claims to the Dashboard view so its partial can render safely.
           var coordinatorClaims = _db.Claims
                .OrderByDescending(c => c.CreatedAt)
                .Take(50)
                .AsNoTracking()
                .ToList(); 

            ViewBag.CoordinatorClaims = coordinatorClaims; 

            // Provide lecturer-specific claims for the lecturer partial
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var lecturerClaims = _db.Claims
                .Where(c => c.LecturerUserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Include(c => c.Documents)
                .AsNoTracking()
                .Take(50)
                .ToList();
            ViewBag.LecturerClaims = lecturerClaims;

            // Manager view uses all recent claims as well
            ViewBag.ManagerClaims = coordinatorClaims;

            return View();
        } 

        // Logout
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult LectureDashboard()
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var claims = _db.Claims
                .Where(c => c.LecturerUserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Include(c => c.Documents)
                .AsNoTracking()
                .Take(50)
                .ToList();
            return View(claims);
        }

        // Return CoordinatorDashboard with a model so direct requests don't hit a null Model.
        public IActionResult CoordinatorDashboard()
        {
            var claims = _db.Claims
                .OrderByDescending((ContractClaim c) => c.CreatedAt)
                .Take(50)
                .AsNoTracking()
                .ToList();

            return View(claims);
        }

        public IActionResult ManagerDashboard()
        {
            var claims = _db.Claims
                .OrderByDescending((ContractClaim c) => c.CreatedAt)
                .Include(c => c.Documents)
                .AsNoTracking()
                .Take(50)
                .ToList();
            return View(claims);
        }

        [HttpGet]
        [Route("Account/AccessDenied")]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View("~/Views/Account/AccessDenied.cshtml");
        }
    }
}