using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.SignalR;

namespace api.Hubs
{
    public class MyHub : Hub
    {
        public async Task SendMessage(string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", message);
        }
    }
}