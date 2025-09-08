using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Contract_Monthly_Claim_System.Controllers
{
    public class HomeController : Controller
    {
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
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, email),
                    new Claim(ClaimTypes.Role, "Lecturer") // Default role for demo
                };
                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity)
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
            return View();
        }

        // Logout
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult LectureDashboard()
        {
            return View();
        }
        public IActionResult CoordinatorDashboard()
        {
            return View();
        }
        public IActionResult ManagerDashboard()
        {
            return View();
        }
    }
}