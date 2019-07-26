using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using IoTLabClassLibrary.Models;

namespace IoTLabWeb.Hubs
{
    public class SensorHub : Hub
    {
        public Task Broadcast(string sender, Measurement measurement)
        {
            return Clients
            
                // Do not Broadcast to Caller:
                .AllExcept(new[] { Context.ConnectionId })
                // Broadcast to all connected clients:
              
              //  .SendAsync("Broadcast", sender, measurement);
                
               .InvokeAsync("Broadcast", sender, measurement);
        }
    }
}
