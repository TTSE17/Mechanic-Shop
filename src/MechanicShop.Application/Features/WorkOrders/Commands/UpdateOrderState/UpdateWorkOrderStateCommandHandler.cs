using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Enums;
using MechanicShop.Domain.Workorders.Events;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.UpdateOrderState;

public class UpdateWorkOrderStateCommandHandler(
    ILogger<UpdateWorkOrderStateCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache,
    TimeProvider dateTime) : IRequestHandler<UpdateWorkOrderStateCommand, Result<Updated>>
{
    public async Task<Result<Updated>> Handle(UpdateWorkOrderStateCommand command, CancellationToken ct)
    {
        var workOrder = await context.WorkOrders.FirstOrDefaultAsync(a => a.Id == command.WorkOrderId, ct);

        if (workOrder is null)
        {
            logger.LogError("WorkOrder with Id '{WorkOrderId}' does not exist.", command.WorkOrderId);

            return ApplicationErrors.WorkOrderNotFound;
        }

        if (workOrder.StartAtUtc > dateTime.GetUtcNow())
        {
            logger.LogError(
                "State transition for WorkOrder Id '{WorkOrderId}` is not allowed before the work order�s scheduled start time.",
                command.WorkOrderId);

            return WorkOrderErrors.StateTransitionNotAllowed(workOrder.StartAtUtc);
        }

        var updateStatusResult = workOrder.UpdateState(command.State);

        if (updateStatusResult.IsError)
        {
            logger.LogError("Failed to update status: {Error}", updateStatusResult.TopError.Description);

            return updateStatusResult.Errors;
        }

        if (command.State == WorkOrderState.Completed)
        {
            // Raise a domain event
            workOrder.AddDomainEvent(new WorkOrderCompleted { WorkOrderId = command.WorkOrderId });
        }

        await context.SaveChangesAsync(ct); // Domain Events Dispatch

        workOrder.AddDomainEvent(new WorkOrderCollectionModified());

        await cache.RemoveByTagAsync("work-order", ct);

        return Result.Updated;
    }
}