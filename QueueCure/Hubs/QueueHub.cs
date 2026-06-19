using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace QueueCure.Hubs
{
    public class QueueHub : Hub
    {
        // Clients can subscribe to specific doctor updates or general broadcasts
        public async Task JoinDoctorGroup(string doctorId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, doctorId);
        }

        public async Task LeaveDoctorGroup(string doctorId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, doctorId);
        }

        // Generic broad notifications
        public async Task NotifyQueueUpdate()
        {
            await Clients.All.SendAsync("QueueUpdated");
        }
    }
}
