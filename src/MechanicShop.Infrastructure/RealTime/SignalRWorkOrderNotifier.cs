using MechanicShop.Application.Common.Interfaces;

using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.RealTime;

/*
   IHubContext<WorkOrderHub> allows you to:
    Send messages to clients
    From outside the Hub class
*/
public sealed class SignalRWorkOrderNotifier(IHubContext<WorkOrderHub> hubContext) : IWorkOrderNotifier
{
    public Task NotifyWorkOrdersChangedAsync(CancellationToken ct = default) =>
        hubContext.Clients.All.SendAsync("WorkOrdersChanged", ct);
}