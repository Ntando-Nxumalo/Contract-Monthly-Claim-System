#nullable enable
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Hubs;
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Connect to database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=CMCS_Db;Trusted_Connection=True;MultipleActiveResultSets=true";

// Enable sensitive data logging only while debugging locally.
// Remove EnableSensitiveDataLogging() in production.
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString)
           .EnableSensitiveDataLogging()
);

// required so _ViewStart can inject IHttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Add Identity with roles
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedAccount = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Make cookie settings explicit to avoid cookie persistence issues in some browsers/environments.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".CMCS.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied"; // <- changed: point to AccessDenied page
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // for Identity UI if used
builder.Services.AddSignalR(); // For real-time notifications

var app = builder.Build();

// Ensure Documents folder exists
var env = app.Services.GetRequiredService<IWebHostEnvironment>();
var documentsFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "Documents");
if (!Directory.Exists(documentsFolder)) Directory.CreateDirectory(documentsFolder);

// Apply pending EF Core migrations and then seed roles/users at startup.
// Guard with try/catch to log and fail visibly when migration/seeding fails.
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        // Ensure database exists and apply any pending migrations (creates ClaimDocuments table).
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Ensure role names match Authorize attributes in controllers.
        string[] roles = new[] { "Lecturer", "Program Coordinator", "Academic Manager" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var r = await roleManager.CreateAsync(new IdentityRole(role));
                if (!r.Succeeded) logger.LogWarning("Failed to create role {Role}: {Errors}", role, string.Join("; ", r.Errors.Select(e => e.Description)));
            }
        }

        // Optionally create a test coordinator user if none exists
        var adminEmail = builder.Configuration["Seed:CoordinatorEmail"] ?? "coordinator@example.com";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin == null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Default Coordinator",
                EmailConfirmed = true,
                Role = "Program Coordinator"
            };
            var result = await userManager.CreateAsync(admin, builder.Configuration["Seed:CoordinatorPassword"] ?? "P@ssword1");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Program Coordinator");
            }
            else
            {
                logger.LogWarning("Seeding admin user failed: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
    }
    catch (Exception ex)
    {
        var logger2 = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger2.LogError(ex, "Error while migrating or seeding database");
        throw;
    }
}

// Developer exception page in Development so you get full stack traces
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages(); // For Identity pages
app.MapHub<ClaimHub>("/claimHub"); // Real-time hub

app.Run();