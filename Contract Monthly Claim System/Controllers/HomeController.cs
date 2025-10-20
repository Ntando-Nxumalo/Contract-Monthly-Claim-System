using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Contract_Monthly_Claim_System.Hubs;
using Microsoft.AspNetCore.Mvc;
using AuthClaim = System.Security.Claims.Claim;
using ContractClaim = Contract_Monthly_Claim_System.Models.Claim;
using Contract_Monthly_Claim_System.Data;
using Microsoft.EntityFrameworkCore;

namespace Contract_Monthly_Claim_System.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db;

        public HomeController(ApplicationDbContext db)
        {
            _db = db;
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
            // Replace with real authentication logic
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                // Simulate user lookup and password check
                // On success, sign in user
                var claims = new List<AuthClaim>
                {
                    new AuthClaim(System.Security.Claims.ClaimTypes.Name, email),
                    new AuthClaim(System.Security.Claims.ClaimTypes.Role, "Lecturer") // Default role for demo
                };

                // Use Identity's application scheme so we don't require registering a separate "Cookies" scheme.
                var claimsIdentity = new System.Security.Claims.ClaimsIdentity(claims, IdentityConstants.ApplicationScheme);

                await HttpContext.SignInAsync(
                    IdentityConstants.ApplicationScheme,
                    new System.Security.Claims.ClaimsPrincipal(claimsIdentity)
                );

                return RedirectToAction("Dashboard", "Home");
            }

            ViewBag.Error = "Invalid login attempt";
            return View("Index");
        }

        // POST: Home/Register
        [HttpPost]
        public IActionResult Register(string name, string role, string email, string password)
        {
            // Replace with real registration logic
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                // Registration success, redirect to login
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Please fill all required fields";
            return View("Register");
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

            return View();
        } 

        // Logout
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult LectureDashboard()
        {
            return View();
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
            return View();
        }
    }
}