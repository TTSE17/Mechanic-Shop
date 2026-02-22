using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.RealTime;

/*
  A SignalR Hub is:
    A real-time communication endpoint
    Allows the server to push messages to connected clients
    Acts like a WebSocket controller
    Think of it as:“A live channel where clients stay connected and listen for messages.”
 */

public sealed class WorkOrderHub : Hub
{
    public const string HubUrl = "/hubs/workorders";
}