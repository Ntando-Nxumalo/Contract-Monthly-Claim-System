using Microsoft.AspNetCore.Mvc;

namespace Contract_Monthly_Claim_System.Models
{
    public class HRDashboardViewModel : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
