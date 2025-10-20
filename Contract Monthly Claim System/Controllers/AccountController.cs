using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace Contract_Monthly_Claim_System.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        // GET: /Account/Login
        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View("~/Views/Home/Index.cshtml"); // Login view
        }

        public class LoginModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = default!;

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = default!;
            public string? ReturnUrl { get; set; }
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel model)
        {
            if (!ModelState.IsValid) return View("~/Views/Home/Index.cshtml", model);

            // Find user by email first (explicit) so we use the canonical UserName during sign-in.
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                _logger.LogInformation("Login failed: user not found for email {Email}", model.Email);
                ModelState.AddModelError("", "Invalid login attempt.");
                ViewBag.Error = "Invalid login attempt.";
                return View("~/Views/Home/Index.cshtml", model);
            }

            // Ensure we clear any existing cookie state before signing in (helps local dev edge-cases).
            await _signInManager.SignOutAsync();

            // Use the user's actual UserName to sign in — avoids issues when the app stores a different username.
            var result = await _signInManager.PasswordSignInAsync(user.UserName, model.Password, isPersistent: false, lockoutOnFailure: false);

            _logger.LogInformation("PasswordSignInAsync result for {Email}: {Succeeded}, IsLockedOut={IsLockedOut}, IsNotAllowed={IsNotAllowed}, RequiresTwoFactor={RequiresTwoFactor}",
                model.Email, result.Succeeded, result.IsLockedOut, result.IsNotAllowed, result.RequiresTwoFactor);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return LocalRedirect(model.ReturnUrl);

                return RedirectToAction("Dashboard", "Home");
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("User account locked out: {Email}", model.Email);
                ModelState.AddModelError("", "Account is locked out.");
                ViewBag.Error = "Account is locked out.";
            }
            else if (result.IsNotAllowed)
            {
                _logger.LogWarning("Login not allowed for user {Email}. Possibly email confirmation required.", model.Email);
                ModelState.AddModelError("", "Login is not allowed. Please confirm your email or contact an administrator.");
                ViewBag.Error = "Login is not allowed.";
            }
            else if (result.RequiresTwoFactor)
            {
                _logger.LogWarning("Two-factor authentication required for user {Email}.", model.Email);
                ModelState.AddModelError("", "Two-factor authentication is required.");
                ViewBag.Error = "Two-factor authentication is required.";
            }
            else
            {
                _logger.LogInformation("Invalid login attempt for user {Email}.", model.Email);
                ModelState.AddModelError("", "Invalid login attempt.");
                ViewBag.Error = "Invalid login attempt.";
            }

            return View("~/Views/Home/Index.cshtml", model);
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View("~/Views/Home/Register.cshtml");
        }

        public class RegisterModel
        {
            [Required, MaxLength(200)]
            public string FullName { get; set; } = default!;

            [Required, EmailAddress]
            public string Email { get; set; } = default!;

            [Required, DataType(DataType.Password)]
            public string Password { get; set; } = default!;

            [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match.")]
            public string ConfirmPassword { get; set; } = default!;

            [Required]
            public string Role { get; set; } = "Lecturer";
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (!ModelState.IsValid) return View("~/Views/Home/Register.cshtml", model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Role = model.Role
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                // Ensure role exists before adding (roles are seeded in Program.cs, but keep safe)
                await _userManager.AddToRoleAsync(user, model.Role);
                await _signInManager.SignInAsync(user, isPersistent: false);
                _logger.LogInformation("User created and signed in: {Email}", model.Email);
                return RedirectToAction("Dashboard", "Home");
            }

            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            ViewBag.Error = "Registration failed. See errors.";
            _logger.LogWarning("Registration failed for {Email}: {Errors}", model.Email, string.Join("; ", result.Errors.Select(x => x.Description)));
            return View("~/Views/Home/Register.cshtml", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
}
