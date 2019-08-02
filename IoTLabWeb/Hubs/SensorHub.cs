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
            string[] Exceptional = new string[1];
            Exceptional[0] = Context.ConnectionId;
            return Clients
                 // Do not Broadcast to Caller:
                 //.AllExcept(Exceptional).SendAsync("Broadcast", sender, measurement);
                 .All.SendAsync("Broadcast", sender, measurement);
        }
    }
}
