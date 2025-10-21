#nullable enable
using Contract_Monthly_Claim_System.Data;
using Contract_Monthly_Claim_System.Hubs;
using Contract_Monthly_Claim_System.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Connect to database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=CMCS_Db;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

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

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // for Identity UI if used
builder.Services.AddSignalR(); // For real-time notifications

// Configure cookie paths for AccessDenied and Login to existing endpoints
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Home/Index";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Ensure Documents folder exists
var env = app.Services.GetRequiredService<IWebHostEnvironment>();
var documentsFolder = Path.Combine(env.WebRootPath ?? "wwwroot", "Documents");
if (!Directory.Exists(documentsFolder)) Directory.CreateDirectory(documentsFolder);

// Seed roles at startup
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

    // Added "Academic Manager" role so Manager dashboard and hub grouping match seeded roles
    string[] roles = new[] { "Lecturer", "Program Coordinator", "Academic Manager" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
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
    }
}

// Middleware
if (!app.Environment.IsDevelopment())
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
