using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.WorkOrders.Commands.AssignLabor;

public class AssignLaborCommandHandler(
    ILogger<AssignLaborCommandHandler> logger,
    IAppDbContext context,
    HybridCache cache,
    IWorkOrderPolicy workOrderRuleService) : IRequestHandler<AssignLaborCommand, Result<Updated>>
{
    public async Task<Result<Updated>> Handle(AssignLaborCommand command, CancellationToken ct)
    {
        var workOrder = await context.WorkOrders.FirstOrDefaultAsync(a => a.Id == command.WorkOrderId, ct);

        if (workOrder is null)
        {
            logger.LogError("WorkOrder with Id '{WorkOrderId}' does not exist.", command.WorkOrderId);
            return ApplicationErrors.WorkOrderNotFound;
        }

        var labor = await context.Employees.FindAsync([command.LaborId], ct);

        if (labor is null)
        {
            logger.LogError("Invalid LaborId: {LaborId}", command.LaborId);
            return ApplicationErrors.LaborNotFound;
        }

        if (await workOrderRuleService.IsLaborOccupied(command.LaborId, command.WorkOrderId, workOrder.StartAtUtc,
                workOrder.EndAtUtc))
        {
            logger.LogError("Labor with Id '{LaborId}' is already occupied during the requested time.",
                workOrder.LaborId);
            return ApplicationErrors.LaborOccupied;
        }

        var updateLaborResult = workOrder.UpdateLabor(command.LaborId);

        if (updateLaborResult.IsError)
        {
            foreach (var error in updateLaborResult.Errors)
            {
                logger.LogError("[LaborUpdate] {ErrorCode}: {ErrorDescription}", error.Code, error.Description);
            }

            return updateLaborResult.Errors;
        }

        await context.SaveChangesAsync(ct);

        await cache.RemoveByTagAsync("work-order", ct);

        return Result.Updated;
    }
}