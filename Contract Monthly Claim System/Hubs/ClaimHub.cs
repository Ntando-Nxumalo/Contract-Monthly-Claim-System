#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Contract_Monthly_Claim_System.Hubs
{
    // Allow only authenticated users to connect
    [Authorize]
    public class ClaimHub : Hub
    {
        // Server methods can be called by controller to broadcast updates
        public Task NotifyClaimStatusChanged(int claimId, string status)
        {
            return Clients.All.SendAsync("ReceiveClaimStatusUpdate", claimId, status);
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
                var principal = Context.User;

                if (principal?.IsInRole("Program Coordinator") == true || principal?.IsInRole("Academic Manager") == true)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "coordinators");
                }

                if (principal?.IsInRole("HR") == true)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, "hr");
                }

                // Add authenticated user to a per-user group so they receive personal updates
                var userId = Context.UserIdentifier;
                if (!string.IsNullOrEmpty(userId))
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
                }
            }

            await base.OnConnectedAsync();
        }
    }
}
