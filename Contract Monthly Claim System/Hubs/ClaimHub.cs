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
            // If the user is a coordinator, add them to the coordinators group
            if (Context.User?.IsInRole("Academic Coordinator") == true ||
                Context.User?.IsInRole("Program Coordinator") == true)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "coordinators");
            }

            // Optionally add students to a student-specific group, e.g. by user id
            if (Context.User?.Identity?.IsAuthenticated == true)
            {
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
