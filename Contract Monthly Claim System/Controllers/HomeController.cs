using Microsoft.AspNetCore.Mvc;

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
        public IActionResult Login(string email, string password)
        {
            // Add your authentication logic here
            if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                // Successful login logic
                return RedirectToAction("Dashboard", "Home");
            }

            // Failed login
            ViewBag.Error = "Invalid login attempt";
            return View("Index");
        }

        // POST: Home/Register
        [HttpPost]
        public IActionResult Register(string name, string role, string email, string password)
        {
            // Add your registration logic here
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
            {
                // Successful registration logic
                return RedirectToAction("Index", "Home");
            }

            // Failed registration
            ViewBag.Error = "Please fill all required fields";
            return View("Register");
        }

        // Dashboard after successful login
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}